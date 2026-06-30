using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Nexa.App;

/// <summary>파일 항목 종류 (코어 <c>FileKind</c>와 동일 순서: 0=File, 1=Dir, 2=Symlink).</summary>
internal enum NexaFileKind : uint
{
    File = 0,
    Dir = 1,
    Symlink = 2,
}

/// <summary>디렉터리 엔트리(관리형 DTO). 네이티브 포인터 수명과 무관하게 복사 보관.</summary>
internal sealed record DirItem(string Name, NexaFileKind Kind, ulong Size, long ModifiedUnixMs)
{
    /// <summary>표시용 종류 라벨(File/Dir/Symlink).</summary>
    public string KindLabel => Kind.ToString();

    /// <summary>표시용 크기(폴더는 빈 문자열).</summary>
    public string SizeLabel => Kind == NexaFileKind.Dir ? string.Empty : $"{Size:N0} B";
}

/// <summary>
/// Rust 코어(nexa-interop cdylib)의 C ABI 표면에 대한 P/Invoke 바인딩.
/// dll은 빌드 시 <c>core/target/&lt;profile&gt;/nexa_interop.dll</c> 에서 앱 출력 디렉토리로 복사된다
/// (Nexa.App.csproj의 BuildNexaInterop/CopyNexaInterop 타겟). 상세: docs/18.
/// </summary>
internal static class NativeInterop
{
    private const string Dll = "nexa_interop";

    /// <summary>인터롭 ABI 버전(호환성 점검용). 불일치 시 로드 거부 등에 사용 예정.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint nexa_abi_version();

    /// <summary>왕복 PoC: 두 정수의 합을 코어(Rust)에서 계산해 반환.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nexa_poc_add(int a, int b);

    // ── 디렉터리 스트리밍 열거 (핸들 기반) ──────────────────────────────────

    /// <summary>C ABI <c>NexaEntry</c> 미러. <c>Name</c>은 다음 next/close 전까지만 유효한 네이티브 포인터.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NexaEntry
    {
        public IntPtr Name;
        public uint Kind;
        public ulong Size;
        public long ModifiedUnixMs;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_dir_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_dir_next(IntPtr handle, ref NexaEntry entry);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_dir_close(IntPtr handle);

    /// <summary>
    /// 디렉터리를 열거해 엔트리 목록을 반환한다(open→next 반복→close 래핑).
    /// 네이티브 <c>name</c> 포인터는 즉시 관리형 문자열로 복사해 수명 문제를 차단한다.
    /// </summary>
    /// <exception cref="IOException">열기 실패(없는 경로·권한 등).</exception>
    public static IReadOnlyList<DirItem> ReadDir(string path)
    {
        IntPtr handle = nexa_dir_open(path);
        if (handle == IntPtr.Zero)
        {
            throw new IOException($"디렉터리 열기 실패: {path}");
        }

        var items = new List<DirItem>();
        try
        {
            var entry = default(NexaEntry);
            while (nexa_dir_next(handle, ref entry) == 1)
            {
                string name = Marshal.PtrToStringUTF8(entry.Name) ?? string.Empty;
                items.Add(new DirItem(name, (NexaFileKind)entry.Kind, entry.Size, entry.ModifiedUnixMs));
            }
        }
        finally
        {
            nexa_dir_close(handle);
        }
        return items;
    }
}
