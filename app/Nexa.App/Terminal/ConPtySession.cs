using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Nexa.App.Terminal;

/// <summary>
/// ConPTY(의사 콘솔) 세션 — 셸(cmd/powershell/pwsh)을 <b>Windows Pseudo Console</b>로 구동하고 입출력 스트림을 잇는다.
/// 터미널 에뮬레이터의 <b>토대(BP-T1)</b>: 셸이 내보내는 <b>VT 시퀀스 바이트 스트림</b>을 그대로 전달하고(<see cref="Output"/>),
/// 사용자 입력을 셸 stdin으로 보낸다(<see cref="Write"/>). VT 파싱/화면 버퍼/렌더는 상위(TerminalView·후속 슬라이스)가 담당.
/// Windows 10 1809+(우리 최소 17763) 필요.
/// </summary>
public sealed class ConPtySession : IDisposable
{
    private IntPtr _hpc = IntPtr.Zero;                 // HPCON
    private PROCESS_INFORMATION _pi;
    private IntPtr _attrList = IntPtr.Zero;
    private FileStream? _writer;                        // 셸 stdin
    private FileStream? _reader;                        // 셸 stdout/stderr(ConPTY 병합)
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>셸 출력(디코드된 텍스트, VT 시퀀스 포함). <b>백그라운드 스레드</b>에서 발생 — 구독자가 UI로 마샬.</summary>
    public event Action<string>? Output;

    /// <summary>셸 프로세스 종료.</summary>
    public event Action? Exited;

    /// <summary>세션 시작 — 셸을 <paramref name="cols"/>×<paramref name="rows"/> 크기 ConPTY로 구동.</summary>
    public void Start(string commandLine, string? workingDir, short cols, short rows)
    {
        // 입력/출력 파이프. 셸 stdin ← inputRead(ConPTY), 우리는 inputWrite로 씀.
        //                    셸 stdout → outputWrite(ConPTY), 우리는 outputRead로 읽음.
        if (!CreatePipe(out SafeFileHandle inputRead, out SafeFileHandle inputWrite, IntPtr.Zero, 0) ||
            !CreatePipe(out SafeFileHandle outputRead, out SafeFileHandle outputWrite, IntPtr.Zero, 0))
        {
            throw new IOException("ConPTY 파이프 생성 실패");
        }

        int hr = CreatePseudoConsole(new COORD { X = cols, Y = rows }, inputRead, outputWrite, 0, out _hpc);
        if (hr != 0)
        {
            throw new IOException($"CreatePseudoConsole 실패 (hr=0x{hr:X8})");
        }

        // ConPTY가 소유하는 파이프 끝(inputRead·outputWrite)은 우리 쪽 사본을 닫는다 →
        // 셸 종료 시 outputRead에 EOF가 전파되도록.
        inputRead.Dispose();
        outputWrite.Dispose();

        // STARTUPINFOEX에 PSEUDOCONSOLE 속성을 실어 CreateProcess.
        var si = new STARTUPINFOEX();
        si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        IntPtr size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        _attrList = Marshal.AllocHGlobal(size);
        si.lpAttributeList = _attrList;
        if (!InitializeProcThreadAttributeList(_attrList, 1, 0, ref size))
        {
            throw new IOException("InitializeProcThreadAttributeList 실패");
        }
        if (!UpdateProcThreadAttribute(_attrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _hpc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
        {
            throw new IOException("UpdateProcThreadAttribute 실패");
        }

        if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                EXTENDED_STARTUPINFO_PRESENT, IntPtr.Zero, workingDir, ref si, out _pi))
        {
            throw new IOException($"CreateProcess 실패 (셸 실행 불가: {commandLine})");
        }

        _writer = new FileStream(inputWrite, FileAccess.Write);
        _reader = new FileStream(outputRead, FileAccess.Read);

        _ = Task.Run(ReadLoopAsync);
        _ = Task.Run(WaitForExitAsync);
    }

    /// <summary>사용자 입력을 셸 stdin으로 보낸다(UTF-8).</summary>
    public void Write(string text)
    {
        if (_disposed || _writer is null || string.IsNullOrEmpty(text))
        {
            return;
        }
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            _writer.Write(bytes, 0, bytes.Length);
            _writer.Flush();
        }
        catch
        {
            // 쓰기 실패(셸 종료 등)는 격리
        }
    }

    /// <summary>터미널 크기 변경 → ConPTY 리사이즈.</summary>
    public void Resize(short cols, short rows)
    {
        if (_disposed || _hpc == IntPtr.Zero || cols <= 0 || rows <= 0)
        {
            return;
        }
        ResizePseudoConsole(_hpc, new COORD { X = cols, Y = rows });
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();   // 멀티바이트 경계 처리
        var chars = new char[8192];
        try
        {
            while (!_cts.IsCancellationRequested && _reader is not null)
            {
                int read = await _reader.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token);
                if (read <= 0)
                {
                    break;   // EOF = 셸 종료
                }
                int n = decoder.GetChars(buffer, 0, read, chars, 0);
                if (n > 0)
                {
                    Output?.Invoke(new string(chars, 0, n));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // 읽기 실패 격리
        }
    }

    private async Task WaitForExitAsync()
    {
        try
        {
            if (_pi.hProcess != IntPtr.Zero)
            {
                await Task.Run(() => WaitForSingleObject(_pi.hProcess, INFINITE));
                Exited?.Invoke();
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try { _cts.Cancel(); } catch { }

        // 셸 종료.
        try { if (_pi.hProcess != IntPtr.Zero) { TerminateProcess(_pi.hProcess, 0); } } catch { }

        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }

        if (_hpc != IntPtr.Zero) { ClosePseudoConsole(_hpc); _hpc = IntPtr.Zero; }

        if (_attrList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attrList);
            Marshal.FreeHGlobal(_attrList);
            _attrList = IntPtr.Zero;
        }
        try { if (_pi.hThread != IntPtr.Zero) { CloseHandle(_pi.hThread); } } catch { }
        try { if (_pi.hProcess != IntPtr.Zero) { CloseHandle(_pi.hProcess); } } catch { }
        _cts.Dispose();
    }

    /// <summary>사용 가능한 기본 셸 명령줄 — pwsh → powershell → cmd 순.</summary>
    public static string DefaultShell()
    {
        foreach (var (exe, args) in new[] { ("pwsh.exe", ""), ("powershell.exe", ""), ("cmd.exe", "") })
        {
            if (ExistsOnPath(exe))
            {
                return string.IsNullOrEmpty(args) ? exe : $"{exe} {args}";
            }
        }
        return "cmd.exe";
    }

    private static bool ExistsOnPath(string exe)
    {
        string? paths = Environment.GetEnvironmentVariable("PATH");
        if (paths is null)
        {
            return false;
        }
        foreach (var dir in paths.Split(Path.PathSeparator))
        {
            try
            {
                if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, exe)))
                {
                    return true;
                }
            }
            catch
            {
            }
        }
        return false;
    }

    // ── Win32 인터롭 (ConPTY) ─────────────────────────────────────────
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;
    private const uint INFINITE = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X; public short Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string? lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
