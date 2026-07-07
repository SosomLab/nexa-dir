using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Nexa.App;

/// <summary>
/// 고전 Win32 OLE 드롭 수신 폴백(BUG-009) — <b>상승(관리자) 프로세스 전용</b>.
/// WinUI 3의 XAML 드롭 수신(WinRT 드래그 스택)은 프로세스 토큰이 상승이면 IL 일치 여부와 무관하게
/// 인바운드 드래그를 거부한다(플랫폼 제한, microsoft-ui-xaml#7690/#10119). UAC OFF PC는 탐색기에서
/// 실행해도 상승이라 항상 재현. 같은 IL 간 고전 OLE(<c>RegisterDragDrop</c>)는 차단되지 않으므로
/// XAML 콘텐츠 브리지 HWND에 자체 <c>IDropTarget</c>을 등록해 우회한다(Double Commander 동일 방식).
/// 좌표 히트테스트·연산 판정·전송은 호스트(MainWindow) 콜백에 위임. 비상승 실행에서는 등록하지
/// 않는다(XAML 경로 정상 — 이중 처리 방지).
/// </summary>
[ComVisible(true)]
internal sealed class OleDropTarget : OleDropTarget.IDropTarget, IDisposable
{
    // DROPEFFECT_* (ole2) — 호스트 콜백 반환값.
    public const uint EffectNone = 0;
    public const uint EffectCopy = 1;
    public const uint EffectMove = 2;

    /// <summary>드래그 통과 중 연산 판정 — (화면좌표 x·y, MK_* 키 상태, 파일(CF_HDROP) 여부) → DROPEFFECT_*.
    /// 원본이 허용한 효과와 교집합해 커서에 반영된다.</summary>
    public Func<int, int, uint, bool, uint>? OverHandler { get; set; }

    /// <summary>드롭 실행 — (화면좌표 x·y, MK_* 키 상태, CF_HDROP 경로들) → 원본에 보고할 DROPEFFECT_*.
    /// 이동도 앱이 직접 수행(최적화 이동)하면 <see cref="EffectNone"/>을 반환해 원본 측 중복 삭제를 막는다.</summary>
    public Func<int, int, uint, List<string>, uint>? DropHandler { get; set; }

    /// <summary>드래그가 창을 벗어남(하이라이트 해제 등).</summary>
    public Action? LeaveHandler { get; set; }

    private IntPtr _hwnd;
    private List<string> _paths = new();   // DragEnter에서 추출(드롭까지 같은 데이터 오브젝트 — Over 판정용)

    private OleDropTarget() { }

    /// <summary>XAML 콘텐츠 브리지 HWND(없으면 최상위)에 드롭 타깃 등록. 실패 시 null(HRESULT는 로그).</summary>
    public static OleDropTarget? TryRegister(IntPtr topLevelHwnd, Action<string>? log = null)
    {
        int init = OleInitialize(IntPtr.Zero);   // S_OK/S_FALSE 외의 실패는 Register에서 드러난다
        // 드래그 좌표의 실제 수신자는 XAML 콘텐츠의 자식 HWND(DesktopChildSiteBridge).
        // XAML이 걸어둔(상승에서 무력한) 드롭 타깃이 있으면 걷어내고 그 자리에 등록한다
        // (최상위에만 걸면 자식 등록이 먼저 조회되어 도달하지 않음).
        IntPtr hwnd = FindWindowExW(topLevelHwnd, IntPtr.Zero, "Microsoft.UI.Content.DesktopChildSiteBridge", null);
        if (hwnd == IntPtr.Zero)
        {
            hwnd = topLevelHwnd;
        }
        _ = RevokeDragDrop(hwnd);   // 미등록이었다면 DRAGDROP_E_NOTREGISTERED — 무해
        var target = new OleDropTarget { _hwnd = hwnd };
        int hr = RegisterDragDrop(hwnd, target);
        log?.Invoke($"OLE fallback register hwnd=0x{hwnd:X} bridge={hwnd != topLevelHwnd} init=0x{init:X} hr=0x{hr:X8}");
        return hr == 0 ? target : null;
    }

    /// <summary>등록 해제(창 종료 시) — 이후 콜백 없음.</summary>
    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            _ = RevokeDragDrop(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    // ── IDropTarget 콜백 (등록 스레드 = UI 스레드의 메시지 펌프로 들어온다) ─────

    int IDropTarget.DragEnter(IDataObject data, uint keyState, POINTL pt, ref uint effect)
    {
        _paths = ExtractHdropPaths(data);
        effect &= OverHandler?.Invoke(pt.X, pt.Y, keyState, _paths.Count > 0) ?? EffectNone;
        return 0;
    }

    int IDropTarget.DragOver(uint keyState, POINTL pt, ref uint effect)
    {
        effect &= OverHandler?.Invoke(pt.X, pt.Y, keyState, _paths.Count > 0) ?? EffectNone;
        return 0;
    }

    int IDropTarget.DragLeave()
    {
        _paths = new List<string>();
        LeaveHandler?.Invoke();
        return 0;
    }

    int IDropTarget.Drop(IDataObject data, uint keyState, POINTL pt, ref uint effect)
    {
        var paths = ExtractHdropPaths(data);
        if (paths.Count == 0)
        {
            paths = _paths;   // 드롭 시점 재추출 실패 시 Enter 캐시로 보완
        }
        _paths = new List<string>();
        effect = DropHandler?.Invoke(pt.X, pt.Y, keyState, paths) ?? EffectNone;
        return 0;
    }

    /// <summary>데이터 오브젝트의 CF_HDROP에서 파일시스템 경로 추출 — 없거나(가상 항목 등) 실패면 빈 목록.</summary>
    private static List<string> ExtractHdropPaths(IDataObject data)
    {
        var paths = new List<string>();
        var fmt = new FORMATETC
        {
            cfFormat = CfHdrop,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL,
        };
        try
        {
            if (data.QueryGetData(ref fmt) != 0)
            {
                return paths;
            }
            data.GetData(ref fmt, out STGMEDIUM medium);
            try
            {
                uint count = DragQueryFileW(medium.unionmember, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    uint len = DragQueryFileW(medium.unionmember, i, null, 0);
                    if (len == 0)
                    {
                        continue;
                    }
                    var sb = new StringBuilder((int)len + 1);
                    if (DragQueryFileW(medium.unionmember, i, sb, len + 1) > 0)
                    {
                        paths.Add(sb.ToString());
                    }
                }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }
        catch
        {
            // 원본 앱 종료·마샬링 실패 등 → 빈 목록(무동작)
        }
        return paths;
    }

    private const short CfHdrop = 15;   // CF_HDROP

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTL
    {
        public int X;
        public int Y;
    }

    [ComImport, Guid("00000122-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDropTarget
    {
        [PreserveSig] int DragEnter(IDataObject pDataObj, uint grfKeyState, POINTL pt, ref uint pdwEffect);
        [PreserveSig] int DragOver(uint grfKeyState, POINTL pt, ref uint pdwEffect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop(IDataObject pDataObj, uint grfKeyState, POINTL pt, ref uint pdwEffect);
    }

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    private static extern int RegisterDragDrop(IntPtr hwnd, IDropTarget target);

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(IntPtr hwnd);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder? fileName, uint cch);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter, string className, string? windowName);
}
