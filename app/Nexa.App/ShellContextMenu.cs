using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Nexa.App;

/// <summary>
/// 클래식 <b>셸 컨텍스트 메뉴 호스팅</b>(ADR-0005, B-2 S1) — 항목들의 셸 <c>IContextMenu</c>를 HMENU로 받아
/// <c>TrackPopupMenuEx</c>로 표시한다(탐색기 "더 많은 옵션" 메뉴와 동일: 7-Zip·Git·보내기·열기 방법·속성 등).
/// <para><b>고유 항목 병합</b>: 셸에 ID 대역 1~0x7FFF를 주고, 고유 항목은 0x8000+로 같은 HMENU에 추가 —
/// 선택 ID 대역으로 분기(셸=<c>InvokeCommand</c> / 고유=콜백). "보내기"/"열기 방법" 등 동적 서브메뉴는
/// <c>IContextMenu2/3</c> 메시지 포워딩(창 서브클래스, 메뉴 표시 구간에만)으로 채워진다.</para>
/// <para>다중 선택은 <b>같은 부모 폴더</b> 항목만 지원(호출자가 보장) — 교차 부모는 후속(S3). Windows 전용.</para>
/// </summary>
internal sealed class ShellContextMenu
{
    private const uint IdShellFirst = 1;
    private const uint IdShellLast = 0x7FFF;
    /// <summary>고유(호스트) 항목 ID 시작 — 셸 대역과 겹치지 않는다(ADR-0005).</summary>
    public const uint IdCustomFirst = 0x8000;

    /// <summary>병합할 고유 메뉴 항목. <see cref="Id"/>는 0x8000 이상.</summary>
    public sealed record CustomItem(uint Id, string Text, bool Enabled, Action Invoke);

    /// <summary>표시 결과 — 셸 명령 실행 여부를 호출자가 알 수 있게(후처리 갱신 판단용).</summary>
    public enum Result { Cancelled, ShellCommand, CustomCommand }

    private IContextMenu2? _icm2;
    private IContextMenu3? _icm3;
    private SUBCLASSPROC? _subclassProc;   // 메뉴 표시 동안 GC 보호

    /// <summary>셸 메뉴를 커서 위치에 표시. <paramref name="paths"/>는 같은 부모 폴더의 파일/폴더들.
    /// <paramref name="extendedVerbs"/>=Shift(확장 동사). 실패/취소 시 <see cref="Result.Cancelled"/>.</summary>
    public Result Show(IntPtr hwnd, IReadOnlyList<string> paths, IReadOnlyList<CustomItem> custom, bool extendedVerbs = false)
    {
        var fullPidls = new List<IntPtr>();
        IShellFolder? folder = null;
        object? icmObj = null;
        IntPtr hmenu = IntPtr.Zero;
        try
        {
            // 1) 경로 → PIDL → 공통 부모 IShellFolder + child PIDL 목록.
            var children = new List<IntPtr>();
            Guid iidFolder = typeof(IShellFolder).GUID;
            foreach (string p in paths)
            {
                if (SHParseDisplayName(p, IntPtr.Zero, out IntPtr pidl, 0, out _) != 0)
                {
                    continue;   // 접근 불가 항목은 제외(격리)
                }
                fullPidls.Add(pidl);   // child는 full 내부를 가리킴 → full을 메뉴 종료까지 유지
                if (SHBindToParent(pidl, ref iidFolder, out IShellFolder f, out IntPtr child) != 0)
                {
                    continue;
                }
                if (folder is null)
                {
                    folder = f;
                }
                else
                {
                    Marshal.ReleaseComObject(f);   // 같은 부모 — 첫 폴더만 유지
                }
                children.Add(child);
            }
            if (folder is null || children.Count == 0)
            {
                return Result.Cancelled;
            }

            // 2) IContextMenu 취득 + HMENU 채움(셸 대역 1~0x7FFF).
            Guid iidIcm = typeof(IContextMenu).GUID;
            folder.GetUIObjectOf(hwnd, (uint)children.Count, children.ToArray(), ref iidIcm, IntPtr.Zero, out icmObj);
            var icm = (IContextMenu)icmObj;
            _icm2 = icmObj as IContextMenu2;
            _icm3 = icmObj as IContextMenu3;
            hmenu = CreatePopupMenu();
            uint qcmFlags = extendedVerbs ? CMF_EXTENDEDVERBS : CMF_NORMAL;
            if (icm.QueryContextMenu(hmenu, 0, IdShellFirst, IdShellLast, qcmFlags) < 0)
            {
                return Result.Cancelled;
            }

            // 3) 고유 항목 병합(0x8000+) — 구분자로 섹션 분리(ADR-0005).
            if (custom.Count > 0)
            {
                AppendMenuW(hmenu, MF_SEPARATOR, UIntPtr.Zero, null);
                foreach (var c in custom)
                {
                    AppendMenuW(hmenu, MF_STRING | (c.Enabled ? 0u : MF_GRAYED), (UIntPtr)c.Id, c.Text);
                }
            }

            // 4) 표시 — IContextMenu2/3 메시지 포워딩(동적 서브메뉴·owner-draw)을 위해 표시 구간만 서브클래스.
            GetCursorPos(out POINT pt);
            _subclassProc = SubclassProc;
            SetWindowSubclass(hwnd, _subclassProc, 1, UIntPtr.Zero);
            uint sel;
            try
            {
                SetForegroundWindow(hwnd);   // 메뉴 밖 클릭 시 정상 닫힘(표준 관례)
                sel = (uint)TrackPopupMenuEx(hmenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, hwnd, IntPtr.Zero);
                PostMessageW(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                RemoveWindowSubclass(hwnd, _subclassProc, 1);
                _subclassProc = null;
            }
            if (sel == 0)
            {
                return Result.Cancelled;
            }

            // 5) 분기 — 고유 대역이면 콜백, 셸 대역이면 InvokeCommand.
            if (sel >= IdCustomFirst)
            {
                custom.FirstOrDefault(c => c.Id == sel)?.Invoke();
                return Result.CustomCommand;
            }
            var inv = new CMINVOKECOMMANDINFOEX
            {
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                hwnd = hwnd,
                lpVerb = (IntPtr)(sel - IdShellFirst),    // MAKEINTRESOURCE(선택 오프셋)
                lpVerbW = (IntPtr)(sel - IdShellFirst),
                nShow = SW_SHOWNORMAL,
                ptInvoke = pt,
            };
            icm.InvokeCommand(ref inv);
            return Result.ShellCommand;
        }
        catch
        {
            return Result.Cancelled;   // 셸 확장 예외 격리 — 메뉴 실패가 앱을 죽이지 않게(ADR-0005 위험 1)
        }
        finally
        {
            _icm2 = null;
            _icm3 = null;
            if (hmenu != IntPtr.Zero)
            {
                DestroyMenu(hmenu);
            }
            if (icmObj is not null)
            {
                Marshal.ReleaseComObject(icmObj);
            }
            if (folder is not null)
            {
                Marshal.ReleaseComObject(folder);
            }
            foreach (IntPtr pidl in fullPidls)
            {
                Marshal.FreeCoTaskMem(pidl);   // ILFree 동등
            }
        }
    }

    /// <summary>메뉴 표시 구간의 창 서브클래스 — "보내기"/"열기 방법" 지연 채움·owner-draw 아이콘 메시지를
    /// <c>IContextMenu2/3</c>로 포워딩(없으면 기본 처리).</summary>
    private IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg is WM_INITMENUPOPUP or WM_DRAWITEM or WM_MEASUREITEM or WM_MENUCHAR)
        {
            try
            {
                if (_icm3 is not null)
                {
                    _icm3.HandleMenuMsg2(uMsg, wParam, lParam, out IntPtr result);
                    return uMsg == WM_MENUCHAR ? result : IntPtr.Zero;
                }
                if (_icm2 is not null)
                {
                    _icm2.HandleMenuMsg(uMsg, wParam, lParam);
                    return IntPtr.Zero;
                }
            }
            catch
            {
                // 확장 예외 격리 — 메뉴 그리기 실패는 무시
            }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // ── 상수 ─────────────────────────────────────────────────────────
    private const uint CMF_NORMAL = 0x0;
    private const uint CMF_EXTENDEDVERBS = 0x100;
    private const uint CMIC_MASK_UNICODE = 0x4000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;
    private const uint MF_STRING = 0x0;
    private const uint MF_GRAYED = 0x1;
    private const uint MF_SEPARATOR = 0x800;
    private const uint TPM_RETURNCMD = 0x100;
    private const uint TPM_RIGHTBUTTON = 0x2;
    private const int SW_SHOWNORMAL = 1;
    private const uint WM_NULL = 0x0;
    private const uint WM_INITMENUPOPUP = 0x117;
    private const uint WM_DRAWITEM = 0x2B;
    private const uint WM_MEASUREITEM = 0x2C;
    private const uint WM_MENUCHAR = 0x120;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>Unicode 확장 호출 구조(CMINVOKECOMMANDINFOEX). ANSI 문자열 필드는 IntPtr로 두고 미사용.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;
    }

    // ── COM 인터페이스 (수동 vtable — ShellLink.cs 동일 패턴) ─────────
    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        // COM 인터롭은 상속 인터페이스도 vtable 전체 재선언 필요(IContextMenu 부분 포함).
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        [PreserveSig] int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    // ── P/Invoke ─────────────────────────────────────────────────────
    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, ref Guid riid, out IShellFolder ppv, out IntPtr ppidlLast);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
}
