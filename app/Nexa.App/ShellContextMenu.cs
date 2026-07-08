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

    /// <summary>병합할 고유 메뉴 항목. <see cref="Id"/>는 0x8000 이상. <see cref="Children"/>이 있으면
    /// 서브메뉴(팝업)로 렌더 — 이때 자신의 Invoke는 무시(리프만 실행).</summary>
    public sealed record CustomItem(uint Id, string Text, bool Enabled, Action Invoke,
        IReadOnlyList<CustomItem>? Children = null);

    /// <summary>셸 동사를 우리 항목으로 <b>제자리 대체</b> — 셸 메뉴의 해당 verb(예: "copyaspath") 위치에서
    /// 원 항목을 지우고 <see cref="Item"/>을 끼워 넣는다(교차폴더처럼 셸이 못 하는 동작을 같은 자리에서 제공).
    /// verb를 못 찾으면 고유 섹션 하단에 폴백 추가.</summary>
    public sealed record VerbReplacement(string Verb, CustomItem Item);

    /// <summary>표시 결과 — 셸 명령 실행 여부를 호출자가 알 수 있게(후처리 갱신 판단용).</summary>
    public enum Result { Cancelled, ShellCommand, CustomCommand }

    private IContextMenu2? _icm2;
    private IContextMenu3? _icm3;
    private SUBCLASSPROC? _subclassProc;   // 메뉴 표시 동안 GC 보호

    /// <summary>셸 메뉴를 커서 위치에 표시. <paramref name="paths"/>는 같은 부모 폴더의 파일/폴더들.
    /// <paramref name="extendedVerbs"/>=Shift(확장 동사). 실패/취소 시 <see cref="Result.Cancelled"/>.
    /// <paramref name="verbInterceptor"/>: 선택된 셸 명령의 canonical verb(예: "delete")를 넘겨 true 반환 시
    /// 셸 실행 대신 호스트가 처리(undo 기록 등 앱 통합이 필요한 동사 가로채기).
    /// <paramref name="customOnTop"/>: 커스텀 섹션 위치 — false=셸 항목 아래(기본)/true=위(docs/38 §7).</summary>
    public Result Show(IntPtr hwnd, IReadOnlyList<string> paths, IReadOnlyList<CustomItem> custom,
        bool extendedVerbs = false, Func<string?, bool>? verbInterceptor = null, bool customOnTop = false,
        IReadOnlyList<VerbReplacement>? replacements = null)
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
            // 3) 고유 항목 병합(0x8000+) — 구분자로 섹션 분리(ADR-0005). 위치=설정(docs/38 §7). 서브메뉴 지원.
            if (custom.Count > 0 && customOnTop)
            {
                AppendCustomItems(hmenu, custom);
                AppendMenuW(hmenu, MF_SEPARATOR, UIntPtr.Zero, null);
            }
            uint qcmFlags = extendedVerbs ? CMF_EXTENDEDVERBS : CMF_NORMAL;
            uint insertAt = customOnTop ? (uint)(custom.Count + 1) : 0u;   // 셸 항목 삽입 위치(커스텀 뒤)
            if (icm.QueryContextMenu(hmenu, insertAt, IdShellFirst, IdShellLast, qcmFlags) < 0)
            {
                return Result.Cancelled;
            }
            // 3-1) 셸 동사 제자리 대체(copyaspath 등) — verb 위치에서 원 항목 삭제 후 우리 항목 삽입.
            //      못 찾은 것(셸이 그 동사를 안 냄)은 고유 섹션 하단에 폴백.
            var unplaced = new List<CustomItem>();
            if (replacements is { Count: > 0 })
            {
                foreach (var rep in replacements)
                {
                    int pos = FindMenuPosByVerb(hmenu, icm, rep.Verb);
                    if (pos >= 0)
                    {
                        DeleteMenu(hmenu, (uint)pos, MF_BYPOSITION);
                        InsertMenuW(hmenu, (uint)pos, MF_BYPOSITION | MF_STRING | (rep.Item.Enabled ? 0u : MF_GRAYED),
                            (UIntPtr)rep.Item.Id, rep.Item.Text);
                    }
                    else
                    {
                        unplaced.Add(rep.Item);
                    }
                }
            }
            if ((custom.Count > 0 || unplaced.Count > 0) && !customOnTop)
            {
                AppendMenuW(hmenu, MF_SEPARATOR, UIntPtr.Zero, null);
                AppendCustomItems(hmenu, custom);
                AppendCustomItems(hmenu, unplaced);
            }
            else if (unplaced.Count > 0 && customOnTop)
            {
                AppendCustomItems(hmenu, unplaced);
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

            // 5) 분기 — 고유 대역이면 콜백(서브메뉴 재귀 탐색), 셸 대역이면 InvokeCommand.
            if (sel >= IdCustomFirst)
            {
                CustomItem? hit = FindCustom(custom, sel);
                if (hit is null && replacements is not null)
                {
                    hit = FindCustom(replacements.Select(r => r.Item).ToList(), sel);
                }
                hit?.Invoke();
                return Result.CustomCommand;
            }
            // 앱 통합이 필요한 동사(delete 등)는 호스트가 가로채 처리(undo 기록·전송 엔진 합류).
            if (verbInterceptor is not null && verbInterceptor(GetVerb(icm, sel - IdShellFirst)))
            {
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

    /// <summary>고유 항목들을 HMENU에 추가 — Children이 있으면 서브 팝업으로(재귀). DestroyMenu가 서브까지 해제.</summary>
    private static void AppendCustomItems(IntPtr hmenu, IReadOnlyList<CustomItem> items)
    {
        foreach (var c in items)
        {
            if (c.Children is { Count: > 0 })
            {
                IntPtr sub = CreatePopupMenu();
                AppendCustomItems(sub, c.Children);
                AppendMenuW(hmenu, MF_POPUP | (c.Enabled ? 0u : MF_GRAYED), (UIntPtr)sub, c.Text);
            }
            else
            {
                AppendMenuW(hmenu, MF_STRING | (c.Enabled ? 0u : MF_GRAYED), (UIntPtr)c.Id, c.Text);
            }
        }
    }

    /// <summary>선택 ID로 고유 항목(서브메뉴 포함) 탐색.</summary>
    private static CustomItem? FindCustom(IReadOnlyList<CustomItem> items, uint id)
    {
        foreach (var c in items)
        {
            if (c.Id == id)
            {
                return c;
            }
            if (c.Children is { Count: > 0 } && FindCustom(c.Children, id) is CustomItem hit)
            {
                return hit;
            }
        }
        return null;
    }

    /// <summary>HMENU 최상위에서 canonical verb가 <paramref name="verb"/>인 셸 항목의 위치(index). 없으면 -1.
    /// 팝업(서브메뉴, id=0xFFFFFFFF)·구분자·고유 항목(0x8000+)은 건너뜀 — 셸 대역 leaf만 조회.</summary>
    private static int FindMenuPosByVerb(IntPtr hmenu, IContextMenu icm, string verb)
    {
        int count = GetMenuItemCount(hmenu);
        for (int i = 0; i < count; i++)
        {
            uint id = GetMenuItemID(hmenu, i);
            if (id < IdShellFirst || id > IdShellLast)
            {
                continue;   // 팝업/구분자/커스텀 제외
            }
            if (string.Equals(GetVerb(icm, id - IdShellFirst), verb, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>선택된 셸 명령의 canonical verb(언어 무관 식별자, 예: "delete"/"copy"). 미구현 확장은 null.</summary>
    private static string? GetVerb(IContextMenu icm, uint idOffset)
    {
        IntPtr buf = Marshal.AllocHGlobal(1024);
        try
        {
            if (icm.GetCommandString((UIntPtr)idOffset, GCS_VERBW, IntPtr.Zero, buf, 512) == 0)
            {
                return Marshal.PtrToStringUni(buf);
            }
        }
        catch
        {
            // 일부 확장은 GetCommandString 미구현/예외 → 식별 불가(null) — 가로채기 없이 셸 실행
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
        return null;
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
    private const uint GCS_VERBW = 0x4;
    private const uint CMIC_MASK_UNICODE = 0x4000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;
    private const uint MF_STRING = 0x0;
    private const uint MF_GRAYED = 0x1;
    private const uint MF_SEPARATOR = 0x800;
    private const uint MF_POPUP = 0x10;
    private const uint MF_BYPOSITION = 0x400;
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool InsertMenuW(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint GetMenuItemID(IntPtr hMenu, int nPos);

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
