using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Nexa.Controls;
using Nexa.ViewModels;

namespace Nexa.App;

/// <summary>메인 윈도우 — 레이아웃 골격(docs/20) + 좌/우 패널 디렉터리 목록(F4/F5).</summary>
public sealed partial class MainWindow : Window
{
    // 좌/우 패널: 각 PanelView가 상태(탭·활성탭·세대) + UI 참조(그리드·헤더·경로바·탭바)를 묶는다.
    // bool left ? _leftX : _rightX 분기를 Panel(left).X로 통합(감사 B-2). ctor에서 XAML 요소로 초기화.
    // 선택(OrderedSet)·캐럿·펼침은 코어/컬렉션이 소유(C1, docs/29). 활성 탭이 트리 핸들 소유(4-2).
    private PanelView _left = null!;
    private PanelView _right = null!;

    /// <summary>지정 측 패널 반환(좌/우 이중화 분기 소거).</summary>
    private PanelView Panel(bool left) => left ? _left : _right;

    // 패널별 폴더 변경 감시(외부/타 패널 변경 자동 갱신, B-12w). ctor에서 생성.
    private FolderWatcher _leftWatcher = null!;
    private FolderWatcher _rightWatcher = null!;
    private FolderWatcher Watcher(bool left) => left ? _leftWatcher : _rightWatcher;

    /// <summary>지정 패널의 현재 폴더를 감시 대상으로 설정 + 빈 영역 드롭 캡션용 폴더명 갱신(폴더 표시/전환 시 호출).</summary>
    private void ArmWatcher(bool left)
    {
        string cur = Panel(left).Active.Current;
        Watcher(left).Watch(cur);
        Panel(left).Grid.DropTargetName = FolderLabel(cur);   // 빈 영역 드롭 캡션 "…에 복사/이동"(SHELL-DND)
    }

    public MainWindow()
    {
        InitializeComponent();
        // 좌/우 패널이 같은 컬럼 인스턴스를 공유 → 리사이즈가 헤더·본문·양쪽 패널에 동시 반영(A3/A4).
        // 표시 순서 = 이름 · 수정한 날짜 · 종류 · 크기(Finder 스타일).
        foreach (var key in new[] { "ColName", "ColExt", "ColDate", "ColKind", "ColSize" })
        {
            var col = (NexaGridColumn)RootGrid.Resources[key];
            DirGrid.Columns.Add(col);
            DirGrid2.Columns.Add(col);
        }
        // 헤더 클릭 정렬(COL-2c) — 좌/우 독립. 각 그리드는 자기 패널에만 적용(표시도 패널별 HeaderCell).
        DirGrid.SortRequested += d => OnSortRequested(true, d);
        DirGrid2.SortRequested += d => OnSortRequested(false, d);
        // 타입어헤드(docs/32 TA-5): 문자 입력 → 버퍼 → find_prefix → 선택 이동(패널별 버퍼).
        DirGrid.CharacterReceived += (_, e) => OnTypeAhead(true, e);
        DirGrid2.CharacterReceived += (_, e) => OnTypeAhead(false, e);
        // 방향키 이동: UserControl 포커스 경로에 의존하지 않도록 최상위 RootGrid에서 받는다(활성 패널 기준).
        // handledEventsToo=true → 내부 ScrollViewer가 방향키를 먼저 처리(Handled)해도 항상 수신.
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnGridKeyDown), handledEventsToo: true);
        // 마우스 뒤로/앞으로(XButton1/2) → 활성 패널 탭 네비게이션(FR-I2 기본 바인딩, docs/26 §5-4).
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnRootPointerPressed), handledEventsToo: true);
        // 상승(관리자) 실행이면 XAML 드롭 수신이 플랫폼 차단(BUG-009) → 고전 OLE 폴백 등록.
        // 콘텐츠 브리지 HWND가 필요하므로 로드 후 1회(Loaded는 재발화 가능 — 내부에서 가드).
        RootGrid.Loaded += (_, _) => InitElevatedDropFallback();
        Closed += (_, _) =>
        {
            _oleDropFallback?.Dispose();
            _oleDropFallback = null;
        };
        ShowInteropRoundTrip();
        // 좌/우 PanelView 구성(XAML 요소 참조 묶기). 이후 모든 패널 접근은 Panel(left) 경유.
        _left = new PanelView { IsLeft = true, Grid = DirGrid, Header = DirHeader, PathBar = PathBarL, TabStrip = LeftTabs };
        _right = new PanelView { IsLeft = false, Grid = DirGrid2, Header = DirHeader2, PathBar = PathBarR, TabStrip = RightTabs };
        // 패널별 폴더 감시 → 변경 시 그 패널 자동 갱신(외부 앱/타 패널 작업 반영, B-12w).
        _leftWatcher = new FolderWatcher(DispatcherQueue, () => ReloadPanel(true));
        _rightWatcher = new FolderWatcher(DispatcherQueue, () => ReloadPanel(false));
        // 그리드 행 수명 → 아이콘 지연 로드/취소. 행이 화면 밖으로 나가면 큐에서 제거(빠른 스크롤 부하 제한, P6).
        _iconCache = new ShellIconCache(DispatcherQueue);
        foreach (var p in new[] { _left, _right })
        {
            p.Grid.RowRealized += it => { if (it is DirItem d) { _iconCache.Request(d); } };
            p.Grid.RowRecycled += it => { if (it is DirItem d) { _iconCache.Cancel(d); } };
        }
        // 드래그 빈 영역 드롭 → 그 패널 현재 폴더로 이동(좌우 겸용, B-12).
        DirGrid.BodyDropped += e => OnPanelBackgroundDrop(true, e);
        DirGrid2.BodyDropped += e => OnPanelBackgroundDrop(false, e);
        // 빈 영역 드래그 커서/캡션 연산 결정(자기 폴더로 Move는 금지·Copy는 복제 허용·외부=복사, B-14dnd/DND-SELF/DND-EXT).
        DirGrid.BodyDragOperation = e => BackgroundDragOp(true, e);
        DirGrid2.BodyDragOperation = e => BackgroundDragOp(false, e);
        // 드래그 중 탭 위에 머물면 그 탭으로 전환(폴더가 보이게, B-13 · 시간=설정 TabDwellMs, B-15h).
        _tabDwellTimer.Interval = TimeSpan.FromMilliseconds(AppSettings.View.TabDwellMs);
        _tabDwellTimer.Tick += (_, _) =>
        {
            _tabDwellTimer.Stop();
            if (_tabDwellTarget is PanelTab t)
            {
                SwitchToTab(_left.Tabs.Contains(t), t);
            }
        };
        // 드래그 중 폴더 위에 머물면 그 폴더로 진입(spring-load, 시간=설정 FolderDwellMs, B-15h).
        _folderDwellTimer.Interval = TimeSpan.FromMilliseconds(AppSettings.View.FolderDwellMs);
        _folderDwellTimer.Tick += (_, _) =>
        {
            _folderDwellTimer.Stop();
            if (_folderDwellTarget is DirItem f && f.IsDir)
            {
                Navigate(_folderDwellLeft, f.FullPath, record: true);   // 그 폴더로 진입(계속 드래그해 하위에 드롭)
            }
        };
        // 이름변경 지연 트리거: 더블클릭 시간 경과(그 사이 더블클릭 없음) → 이름변경 시작(더블클릭 실행과 구분).
        _renameDelayTimer.Tick += (_, _) =>
        {
            _renameDelayTimer.Stop();
            if (_renamePendingItem is DirItem it && _renamePendingRow is FrameworkElement row && !it.IsRenaming)
            {
                BeginRename(row, it, _renamePendingLeft);
            }
            _renamePendingItem = null;
            _renamePendingRow = null;
        };
        // 패널별 초기 탭 1개 + 탭 바 바인딩(멀티라인·고정크기 ItemsRepeater).
        _left.Active.IsActive = true;
        _right.Active.IsActive = true;
        _left.Tabs.Add(_left.Active);
        _right.Tabs.Add(_right.Active);
        _left.TabStrip.ItemsSource = _left.Tabs;
        _right.TabStrip.ItemsSource = _right.Tabs;
        // 경로 바(브레드크럼/편집) 이동 요청 → 실제 네비게이션(존재 확인 후). 좌/우 각각.
        PathBarL.Navigated += (_, e) => OnPathBarNavigated(true, e.Path);
        PathBarR.Navigated += (_, e) => OnPathBarNavigated(false, e.Path);
        // 마지막 세션(탭 상태) 복원, 없으면 기본 시작(좌=홈·우=문서).
        RestoreOrDefaultSession();
        UpdateBottomDock();
        RefreshBottomDocks();   // 하단 도킹 초기 정보 (BP-1)
        // 하단 패널 콘텐츠 종류 변경 → 세션 저장 예약 (BP-1c)
        BottomLeftDockView.KindChanged += (_, _) => _session?.MarkDirty();
        BottomRightDockView.KindChanged += (_, _) => _session?.MarkDirty();
        Activated += OnWindowActivated;   // 윈도우 포커스 상실 시 선택 회색화
        // 외부 앱이 클립보드를 바꾸면(다른 파일/텍스트 복사) 앱 내부 클립보드는 낡음 → 비워서 최신(OS 클립보드) 우선.
        try { Clipboard.ContentChanged += (_, _) => FileClipboard.Clear(); } catch { /* 클립보드 미가용 격리 */ }

        // 세션 저장 엔진 가동(일반 설정과 별도 파일 session.json) — 디바운스+유휴+주기+종료 flush(급종료 대비).
        // 복원 이후에 생성 → 복원 중 MarkDirty 소음 없음(훅은 _session? 널가드).
        _session = new SessionStore(SessionStore.DefaultPath(), DispatcherQueue, CaptureSession);
        Closed += (_, _) => _session?.Flush();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _session?.Flush();
    }

    // ── 세션(탭) 상태 영속화 — session.json (SessionStore) ────────────────────
    // 저장 대상: 활성 패널 · 패널별(활성 탭 인덱스 · 열린 탭 목록[경로·펼침 집합·정렬]). 상세는 SessionStore.cs.
    private SessionStore? _session;

    /// <summary>현재 좌/우 패널·탭 상태를 세션 스냅샷으로 캡처(저장용). UI 스레드에서 호출.</summary>
    private SessionState CaptureSession() => new()
    {
        ActiveLeft = _activeLeft,
        Left = CapturePanel(_left),
        Right = CapturePanel(_right),
        Bottom = CaptureBottom(),
    };

    /// <summary>하단 도킹 패널 상태 캡처(표시/높이/분리/콘텐츠 종류) — BP-1c.</summary>
    private BottomPanelState CaptureBottom() => new()
    {
        Visible = ToggleTerminalBtn.IsChecked == true,
        Height = TerminalPanel.Visibility == Visibility.Visible && TermRow.ActualHeight > 40
            ? TermRow.ActualHeight
            : 180,
        Split = ToggleBottomSplitBtn.IsChecked == true,
        LeftKind = (int)BottomLeftDockView.Kind,
        RightKind = (int)BottomRightDockView.Kind,
    };

    /// <summary>패널 하나의 탭 목록·정렬을 세션 형태로 캡처. (정렬은 현재 패널 단위 → 각 탭에 동일 기록.)</summary>
    private static PanelSession CapturePanel(PanelView p)
    {
        var sort = new List<SortKeyState>();
        foreach (var k in p.SortKeys ?? Array.Empty<NativeInterop.NexaSortKey>())
        {
            sort.Add(new SortKeyState { Key = k.Key, Descending = k.Desc != 0 });
        }
        var sess = new PanelSession { ActiveTab = Math.Max(0, p.Tabs.IndexOf(p.Active)) };
        foreach (var t in p.Tabs)
        {
            if (string.IsNullOrEmpty(t.Current))
            {
                continue;
            }
            sess.Tabs.Add(new TabSession
            {
                Path = t.Current,
                Expanded = new List<string>(t.Expanded),
                Sort = sort,
            });
        }
        return sess;
    }

    /// <summary>세션 파일이 있으면 마지막 탭 상태를 복원, 없으면 기본 시작(좌=홈·우=문서).</summary>
    private void RestoreOrDefaultSession()
    {
        var s = SessionStore.Load(SessionStore.DefaultPath());
        bool restored = false;
        if (s is not null)
        {
            bool l = RestorePanel(true, s.Left);
            bool r = RestorePanel(false, s.Right);
            restored = l || r;
            if (restored)
            {
                SetActivePanel(s.ActiveLeft);
            }
            RestoreBottom(s.Bottom);   // 하단 패널 상태 복원(탭 복원과 독립, BP-1c)
        }
        if (!restored)
        {
            Navigate(true, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), record: false);
            Navigate(false, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), record: false);
        }
    }

    /// <summary>하단 도킹 패널 상태 복원(표시/높이/분리/콘텐츠 종류) — BP-1c.</summary>
    private void RestoreBottom(BottomPanelState b)
    {
        BottomLeftDockView.Kind = ValidKind(b.LeftKind);
        BottomRightDockView.Kind = ValidKind(b.RightKind);
        ToggleBottomSplitBtn.IsChecked = b.Split;
        ToggleTerminalBtn.IsChecked = b.Visible;
        OnToggleTerminal(ToggleTerminalBtn, null!);   // 표시/숨김 반영(+UpdateBottomDock)
        if (b.Visible && b.Height > 40)
        {
            TermRow.Height = new GridLength(b.Height);   // 저장된 높이 복원(토글이 180으로 덮은 뒤)
        }
    }

    private static BottomPanelKind ValidKind(int k) =>
        System.Enum.IsDefined(typeof(BottomPanelKind), k) ? (BottomPanelKind)k : BottomPanelKind.Info;

    /// <summary>패널 하나를 세션에서 복원(탭 목록·펼침·정렬·활성 탭). 존재하는 폴더 탭이 하나도 없으면 false.</summary>
    private bool RestorePanel(bool left, PanelSession ps)
    {
        // 존재하는 폴더만 탭으로 복원(삭제/이동된 경로는 제외).
        var valid = ps.Tabs.Where(t => !string.IsNullOrEmpty(t.Path) && Directory.Exists(t.Path)).ToList();
        if (valid.Count == 0)
        {
            return false;
        }
        var p = Panel(left);
        int activeIdx = Math.Clamp(ps.ActiveTab, 0, valid.Count - 1);
        // 패널 정렬 복원(현재 아키텍처=패널 단위 → 활성 탭 스냅샷의 정렬 사용).
        var sortSrc = valid[activeIdx].Sort;
        p.SortKeys = sortSrc.Count == 0
            ? null
            : sortSrc.Select(k => new NativeInterop.NexaSortKey(k.Key, k.Descending)).ToArray();

        // 기존 기본 탭(ctor에서 1개) 정리 후 세션 탭으로 재구성.
        foreach (var old in p.Tabs)
        {
            old.Items.Dispose();
        }
        p.Tabs.Clear();
        foreach (var t in valid)
        {
            var tab = new PanelTab();
            tab.Nav.NavigateTo(t.Path, record: false);
            tab.Title = PathDisplay.TabTitle(t.Path);
            foreach (var ex in t.Expanded)
            {
                tab.Expanded.Add(ex);
            }
            p.Tabs.Add(tab);
        }
        var activeTab = p.Tabs[activeIdx];
        p.Active = activeTab;
        foreach (var tab in p.Tabs)
        {
            tab.IsActive = ReferenceEquals(tab, activeTab);
        }
        LoadDirectory(left, activeTab);   // 활성 탭만 즉시 로드(펼침·정렬 적용) · 나머지는 전환 시 지연 로드
        return true;
    }

    /// <summary>
    /// 인터롭 왕복 PoC — Rust 코어(nexa-interop)를 P/Invoke로 호출해 결과를 표시한다.
    /// ABI 버전 점검 + nexa_poc_add(2,3) 왕복. 실패 시 메시지로 격리(앱은 계속 동작).
    /// </summary>
    private void ShowInteropRoundTrip()
    {
        try
        {
            NativeInterop.VerifyAbi();   // ABI 버전 + 구조체 레이아웃 검사(불일치 시 예외)
            uint abi = NativeInterop.nexa_abi_version();
            int sum = NativeInterop.nexa_poc_add(2, 3);
            StatusText.Text = $"인터롭 OK — abi={abi}, nexa_poc_add(2, 3)={sum}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"인터롭 실패: {ex.Message}";
        }
    }

    // ── 헤더 정렬 (COL-2c) — 좌/우 독립 ──────────────────────────────────

    /// <summary>컬럼 키 문자열 → 코어 정렬 키 코드(0=Name 1=Ext 2=Size 3=Modified 4=Kind 5=None).</summary>
    private static uint SortKeyCode(string key) => key switch
    {
        "name" => 0,
        "ext" => 1,
        "size" => 2,
        "modified" => 3,
        "kind" => 4,
        _ => 5,
    };

    /// <summary>
    /// 헤더 클릭 정렬 요청 → 코어 정렬 키로 매핑하고 <b>클릭한 패널에만</b> 적용(좌/우 독립).
    /// 패널별 <see cref="PanelView.SortKeys"/>에 저장해 폴더 이동/탭 전환 시 그 패널 핸들에 재적용(지속).
    /// 빈 목록(3상태 "없음") = 빈 배열 저장 → 열거 순서. (컬럼은 공유하나 정렬 상태는 HeaderCell·패널별.)
    /// </summary>
    private void OnSortRequested(bool left, IReadOnlyList<SortDescriptor> descs)
    {
        var keys = descs
            .Select(d => new NativeInterop.NexaSortKey(SortKeyCode(d.Key), d.Descending))
            .ToArray();
        Panel(left).SortKeys = keys;   // 빈 배열=명시적 "없음"(열거 순서), null과 구분
        Panel(left).Active.Items.SetSort(keys, foldersFirst: true);
        _session?.MarkDirty();   // 정렬 변경 → 세션 저장 예약
    }

    // ── 타입어헤드 찾기 (docs/32 TA-5) ───────────────────────────────────

    /// <summary>
    /// 그리드 문자 입력 → 타입어헤드. 버퍼(<see cref="PanelView.TypeAhead"/>)에 누적하고 코어
    /// <c>find_prefix</c>로 매치 가시 인덱스를 찾아 <b>단일 선택+캐럿+스크롤</b>. 편집 중·수정키(Ctrl/Alt)·
    /// Space·제어문자는 제외(선택 토글 등은 <see cref="OnGridKeyDown"/>가 처리). 범위/타임아웃=설정값.
    /// </summary>
    private void OnTypeAhead(bool left, CharacterReceivedRoutedEventArgs e)
    {
        // 이름 편집 박스가 포커스면(편집 중) 타입어헤드 금지 — 편집 텍스트가 처리.
        if (e.OriginalSource is TextBox || IsCtrlDown() || IsAltDown())
        {
            return;
        }
        var panel = Panel(left);
        char c = e.Character;
        long now = Environment.TickCount64;
        string prefix;
        if (c == '\b')                    // Backspace = 접두사 축소
        {
            prefix = panel.TypeAhead.Backspace(now);
        }
        else if (c == ' ' || c < ' ')     // Space 제외 + 제어문자 제외
        {
            return;
        }
        else
        {
            prefix = panel.TypeAhead.Push(c, now);
        }
        if (prefix.Length == 0)
        {
            return;   // 빈 접두사(전부 지움) → 무동작
        }
        int caret = panel.Items.CaretIndex;
        // 확장(refine)=현재 캐럿 포함(캐럿-1로 시작), 새 시작·반복키=캐럿 다음(이동/cycle).
        int searchCaret = panel.TypeAhead.IsExtend ? (caret >= 0 ? caret - 1 : -1) : caret;
        int hit = panel.Items.FindPrefix(searchCaret, prefix, AppSettings.View.TypeAheadScope);
        if (hit >= 0)
        {
            SetActivePanel(left);
            panel.Items.Select(panel.Items[hit], 0);   // 단일 선택
            panel.Items.SetCaret(hit);
            panel.Grid.BringIndexIntoView(hit);
            UpdateSelectionCount(panel.Items);
        }
        e.Handled = true;
    }

    /// <summary>
    /// 코어 트리(nexa-tree)를 <b>백그라운드 스레드</b>에서 열어(<c>OpenAndExpand</c>: 열거+펼침 재적용)
    /// 폴더 내용을 지정 패널 목록에 표시한다. 열거·펼침이 UI 스레드를 블록하지 않으므로 수만 항목 폴더
    /// 진입에도 프리즈가 없다(감사 P1, NFR-P1/R5). 완료는 <c>await</c> 연속(UI 스레드)에서 핸들 채택 +
    /// <paramref name="onLoaded"/>(로드 후 동작: GoUp 선택 등) 실행. 로드 중 같은 패널이 다시 이동하면
    /// 세대 가드로 이전 결과를 폐기한다. 좌/우 패널이 같은 로직 공유. 실패는 헤더 메시지로 격리.
    /// 목록은 <see cref="VirtualTreeCollection"/> — 코어 트리의 가시 노드 평면 스트림을 가상화로 소비한다
    /// (보이는 행만 실체화). 가시성 필터(숨김/점)는 코어에 적용(F24).
    /// </summary>
    private async void LoadDirectory(bool left, PanelTab tab, Action? onLoaded = null)
    {
        var (grid, header, pathBar) = PanelUi(left);
        int gen = ++Panel(left).LoadGen;
        var v = AppSettings.View;
        string path = tab.Current;
        var expanded = new List<string>(tab.Expanded);   // 백그라운드용 스냅샷(UI 스레드 컬렉션 보호)
        header.Text = $"{path} — 여는 중…";
        (IntPtr Handle, int DirectCount) result;
        try
        {
            // 열거+펼침(전체 read_dir + metadata syscall + 정렬)을 백그라운드로 오프로드 → UI 무블록.
            var sortKeys = Panel(left).SortKeys;   // 이 패널 정렬 스냅샷(null=코어 기본, 배열=지정/열거)
            result = await Task.Run(() =>
                VirtualTreeCollection.OpenAndExpand(path, v.ShowHiddenFiles, v.ShowDotFiles, expanded, sortKeys));
        }
        catch (Exception ex)
        {
            if (gen == Panel(left).LoadGen)
            {
                tab.Loaded = false;
                header.Text = $"디렉터리 열거 실패: {ex.Message}";
            }
            return;
        }
        // 재진입: 로드 중 같은 패널이 다시 이동했으면 이 결과를 폐기(방금 연 핸들 정리, UI 미변경).
        if (gen != Panel(left).LoadGen)
        {
            NativeInterop.TreeClose(result.Handle);
            return;
        }
        bool ok = result.Handle != IntPtr.Zero;
        tab.Items.AdoptHandle(result.Handle, path);   // 이전 핸들 Close + 새 핸들 채택 + Reset
        grid.ItemsSource = tab.Items;
        pathBar.Path = path;
        tab.Loaded = ok;                 // 캐시: 성공 시 이 탭은 열린 상태(전환 시 재-Open 불요)
        tab.DirectChildCount = result.DirectCount;
        grid.ScrollToTop();   // 진입 시 첫 항목이 맨 위(이전 폴더 스크롤 오프셋 잔존 방지). GoUp은 onLoaded의 SelectByPath가 대상 정렬로 덮어씀.
        header.Text = ok
            ? $"{path} — {result.DirectCount}개 항목"
            : $"디렉터리 열기 실패: {path}";
        if (ok)
        {
            if (ReferenceEquals(Panel(left).Active, tab))
            {
                ArmWatcher(left);   // 활성 탭 폴더 변경 감시(B-12w)
            }
            onLoaded?.Invoke();   // 로드 완료 후 동작(GoUp 선택·경로바 파일 선택 등). 스크롤 타이밍은 ScrollIndexIntoView가 자체 처리.
        }
    }

    /// <summary>패널 측(좌/우)의 UI 3종(그리드·헤더·경로바)을 묶어 반환.</summary>
    private (NexaFileGrid grid, TextBlock header, NexaPathBar pathBar) PanelUi(bool left)
    {
        var p = Panel(left);
        return (p.Grid, p.Header, p.PathBar);
    }

    // 셸 아이콘 로더(전역 단일): 종류/확장자 LRU 캐시 + 속도 제한 로딩 큐(감사 P6). ctor에서 UI 큐로 생성.
    // 로드/취소는 그리드 행 수명(RowRealized/RowRecycled, ctor에서 배선)에 연결 — 뷰포트 밖 행은 큐에서 제거.
    private readonly ShellIconCache _iconCache;

    // ── 표시(가시성) 토글: 숨김 파일 · 점(.) 파일 (독립·동시 설정, 체크 ON=표시) ─────

    /// <summary>"숨김 파일 보기" 토글(체크=표시) → 숨김 속성 파일 표시 여부 갱신 후 양쪽 패널 재로드.</summary>
    private void OnToggleShowHidden(object sender, EventArgs e)
    {
        AppSettings.View.ShowHiddenFiles = (sender as NexaMenuEntry)?.IsChecked ?? !AppSettings.View.ShowHiddenFiles;
        ReloadBothPanels();
    }

    /// <summary>"점(.) 파일 보기" 토글(체크=표시) → 리눅스식 점 파일 표시 여부 갱신 후 양쪽 패널 재로드.</summary>
    private void OnToggleShowDotFiles(object sender, EventArgs e)
    {
        AppSettings.View.ShowDotFiles = (sender as NexaMenuEntry)?.IsChecked ?? !AppSettings.View.ShowDotFiles;
        ReloadBothPanels();
    }

    /// <summary>현재 경로 기준으로 좌·우 패널을 다시 열거한다(펼침 상태 유지). 설정 토글(가시성) 반영용.</summary>
    private void ReloadBothPanels()
    {
        // 필터가 바뀌었으므로 모든 탭의 캐시를 무효화 — 비활성 탭도 다음 전환 때 재-Open.
        foreach (var t in _left.Tabs) { t.Loaded = false; }
        foreach (var t in _right.Tabs) { t.Loaded = false; }
        if (!string.IsNullOrEmpty(_left.Active.Current))
        {
            LoadDirectory(true, _left.Active);
        }
        if (!string.IsNullOrEmpty(_right.Active.Current))
        {
            LoadDirectory(false, _right.Active);
        }
    }

    // ── 네비게이션(패널/탭별 이동 기록: 뒤로·앞으로·위로) ─────────────

    /// <summary>
    /// 지정 패널을 <paramref name="path"/>로 이동한다. record=true면 현재 위치를 뒤로 스택에 기록(앞으로 초기화).
    /// 로드는 비동기이므로, 로드 완료 후 실행할 동작(예: GoUp의 대상 선택)은 <paramref name="onLoaded"/>로 전달한다.
    /// </summary>
    private void Navigate(bool left, string path, bool record, Action? onLoaded = null)
    {
        Panel(left).Active.Nav.NavigateTo(path, record);   // 이동 기록 갱신(순수 로직 위임)
        ShowCurrent(left, onLoaded);
    }

    private void GoBack(bool left)
    {
        if (Panel(left).Active.Nav.GoBack() is not null)
        {
            ShowCurrent(left);
        }
    }

    private void GoForward(bool left)
    {
        if (Panel(left).Active.Nav.GoForward() is not null)
        {
            ShowCurrent(left);
        }
    }

    /// <summary>활성 탭의 <see cref="NavigationHistory.Current"/> 경로를 화면에 로드한다(제목·재-Open·네비버튼 갱신).
    /// Navigate/GoBack/GoForward 공통 종단 — 이동 기록은 이미 갱신된 상태.</summary>
    private void ShowCurrent(bool left, Action? onLoaded = null)
    {
        var nav = Panel(left).Active;
        nav.Title = PathDisplay.TabTitle(nav.Current);   // 활성 탭 이름 갱신(탭 바 표시)
        nav.Loaded = false;                              // 새 경로 → 재-Open 필요(탭 캐시 무효화)
        LoadDirectory(left, nav, onLoaded);
        UpdateNavButtons(left);
        RefreshBottomDocks();    // 하단 도킹 정보(현재 폴더) 갱신 (BP-1)
        _session?.MarkDirty();   // 경로 변경 → 세션 저장 예약(디바운스)
    }

    private void GoUp(bool left)
    {
        var nav = Panel(left).Active;
        var from = nav.Current;   // 떠나는 폴더(=나) — 상위에서 이 폴더를 선택 상태로
        var parent = Directory.GetParent(from);
        if (parent is not null)
        {
            // 상위 목록은 비동기 로드 → 방금 떠난 폴더 선택·포커스는 로드 완료 후에 실행.
            Navigate(left, parent.FullName, record: true, onLoaded: () =>
            {
                SetActivePanel(left);       // 그 패널을 활성으로(선택 포커스 색)
                SelectByPath(left, from);   // 상위 목록에서 방금 떠난 폴더를 선택·포커스
            });
        }
    }

    /// <summary>지정 패널 목록에서 경로가 일치하는 항목을 단일 선택하고 캐럿·스크롤을 맞춘다(없으면 무시).</summary>
    private void SelectByPath(bool left, string fullPath)
    {
        var items = Panel(left).Items;
        int i = items.IndexOfPath(fullPath);
        if (i < 0)
        {
            return;
        }
        items.Select(items[i], 0);   // 단일 선택(코어 위임)
        items.SetCaret(i);
        // 방금 떠난 폴더(네비 대상)를 화면에 보이게 — 기본 가운데(설정 UpNavTargetAlign). 오프스크린도 강제 실체화.
        Panel(left).Grid.ScrollIndexIntoView(i, AppSettings.View.UpNavTargetAlign);
        UpdateSelectionCount(items);
        RefreshSelectionFocus();
    }

    /// <summary>뒤로/앞으로/위로 버튼 활성 상태 갱신.</summary>
    private void UpdateNavButtons(bool left)
    {
        var nav = Panel(left).Active;
        bool up = !string.IsNullOrEmpty(nav.Current) && Directory.GetParent(nav.Current) is not null;
        if (left)
        {
            BackBtnL.IsEnabled = nav.Nav.CanGoBack;
            FwdBtnL.IsEnabled = nav.Nav.CanGoForward;
            UpBtnL.IsEnabled = up;
        }
        else
        {
            BackBtnR.IsEnabled = nav.Nav.CanGoBack;
            FwdBtnR.IsEnabled = nav.Nav.CanGoForward;
            UpBtnR.IsEnabled = up;
        }
    }

    private static bool IsLeftPanel(object sender) => sender is FrameworkElement fe && (fe.Tag as string) == "L";

    private void OnNavBack(object sender, RoutedEventArgs e) => GoBack(IsLeftPanel(sender));

    private void OnNavForward(object sender, RoutedEventArgs e) => GoForward(IsLeftPanel(sender));

    private void OnNavUp(object sender, RoutedEventArgs e) => GoUp(IsLeftPanel(sender));

    /// <summary>
    /// 마우스 뒤로/앞으로 버튼(XButton1=뒤로, XButton2=앞으로) → <b>포인터가 위치한 패널</b>의
    /// 뒤로/앞으로. 그 패널을 활성으로 만든 뒤 네비 바 버튼과 동일 동작
    /// (<see cref="GoBack"/>/<see cref="GoForward"/>)을 수행한다. 포인터 위치 기준이므로
    /// <b>빈(항목 0개) 폴더에서도 동작</b>한다(행 클릭에 의존하던 활성 패널 판정의 사각지대 제거).
    /// 이 마우스 바인딩은 단축키 시스템(FR-I2, docs/26 §5-4)의 기본 바인딩 — 설정에서 재정의 예정.
    /// </summary>
    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint((UIElement)sender).Properties;
        // 이름변경 중 편집기 밖 press = 커밋(탐색기 동일). 빈 영역/툴바 등 포커스 불가 요소는 클릭해도
        // LostFocus가 안 떠 편집이 유지되던 것 → 그리드로 포커스를 옮겨 LostFocus 커밋 경로를 태운다.
        if (_renamingItem is not null && !IsWithinRenameEditor(e.OriginalSource as DependencyObject))
        {
            Panel(_activeLeft).Grid.Focus(FocusState.Programmatic);   // → OnRenameLostFocus → CommitRename
        }
        // 좌/우클릭 등 일반 press → 포인터가 놓인 패널을 활성화(행이든 빈 영역이든, #4).
        if ((props.IsLeftButtonPressed || props.IsRightButtonPressed)
            && PanelUnderPointer(e.OriginalSource as DependencyObject) is bool paneLeft
            && _activeLeft != paneLeft)
        {
            SetActivePanel(paneLeft);
        }
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        if (!props.IsXButton1Pressed && !props.IsXButton2Pressed)
        {
            return;
        }
        // 포인터가 놓인 패널을 대상으로(빈 목록이어도 판정 가능) → 활성화 후 이동.
        // 어느 패널도 아니면(툴바/메뉴 위 등) 마지막 활성 패널로 폴백.
        bool left = PanelUnderPointer(e.OriginalSource as DependencyObject) ?? _activeLeft;
        SetActivePanel(left);
        if (props.IsXButton1Pressed)
        {
            GoBack(left);
        }
        else
        {
            GoForward(left);
        }
        e.Handled = true;
    }

    /// <summary>포인터 원본 요소가 좌(<c>LeftPaneRoot</c>)/우(<c>RightPanel</c>) 어느 패널 안인지. 둘 다 아니면 null.</summary>
    private bool? PanelUnderPointer(DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, LeftPaneRoot))
            {
                return true;
            }
            if (ReferenceEquals(node, RightPanel))
            {
                return false;
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>
    /// 경로 바(세그먼트 클릭·편집 제출) 이동 요청.
    /// 폴더면 이동. **끝이 파일이면(파일 존재) 파일명을 제외한 상위 폴더로 이동 후 그 파일 선택.**
    /// 둘 다 아니면 상태바 안내(입력 무시·현재 경로 복귀).
    /// </summary>
    private void OnPathBarNavigated(bool left, string path)
    {
        // 환경변수 해석 레이어: %USERPROFILE%(CMD)·$env:USERPROFILE/${env:VAR}(PowerShell) 등을 실제 경로로 확장.
        var p = PathInterpreter.Expand(path);
        if (Directory.Exists(p))
        {
            SetActivePanel(left);
            Navigate(left, p, record: true);   // 폴더면 그대로 이동
            return;
        }
        if (File.Exists(p))
        {
            var dir = System.IO.Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                SetActivePanel(left);
                // 상위 폴더는 비동기 로드 → 그 파일 선택은 로드 완료 후에.
                Navigate(left, dir, record: true, onLoaded: () => SelectByPath(left, p));   // 파일이면 상위 폴더로 이동 후 선택
                return;
            }
        }
        StatusText.Text = $"경로 없음: {p}";
        Panel(left).PathBar.Path = Panel(left).Active.Current;   // 현재 경로로 복귀
    }

    /// <summary>
    /// 폴더 행의 디스클로저(▶/▼)를 눌러 인라인으로 펼치거나 접는다.
    /// 펼침: 해당 폴더의 자식을 바로 아래에 depth+1로 삽입. 접힘: 뒤따르는 더 깊은 행을 제거.
    /// </summary>
    private void OnToggleExpand(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item || !item.IsDir)
        {
            return;
        }
        e.Handled = true;
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        ToggleExpandRow(left, item);
    }

    /// <summary>
    /// 폴더 행 펼침/접힘 토글(디스클로저·키보드 공용). 코어에 위임하고, **탭별 펼침 경로셋**을
    /// 갱신해 진입/이동에도 상태가 유지되게 한다(F18). 펼침/접힘은 코어 diff를 <b>범위 Add/Remove로
    /// 통지</b>하므로 토글 행 위쪽은 제자리·스크롤도 안 튄다 → 이전 E18 오프셋 복원 핵 불요(감사 P2).
    /// </summary>
    private void ToggleExpandRow(bool left, DirItem item)
    {
        if (!item.IsDir)
        {
            return;
        }
        var items = Panel(left).Items;
        var set = Panel(left).Active.Expanded;
        bool willExpand = !item.IsExpanded;
        items.ToggleExpand(item);   // 코어 diff → 범위 Add/Remove 통지(위쪽 행·아이콘·스크롤 보존)
        string key = item.FullPath.TrimEnd('\\', '/');
        if (willExpand)
        {
            set.Add(key);
        }
        else
        {
            set.Remove(key);
        }
        _session?.MarkDirty();   // 펼침/접힘 → 세션 저장 예약
    }

    /// <summary>펼친 목록에서 부모 행 인덱스(현재보다 Depth가 1 작은 최근접 상위 행). 없으면 -1(최상위).</summary>
    private static int ParentIndex(VirtualTreeCollection items, int index)
    {
        if (index < 0)
        {
            return -1;
        }
        int depth = items[index].Depth;
        if (depth <= 0)
        {
            return -1; // 목록 최상위(부모 행이 목록에 없음)
        }
        for (int i = index - 1; i >= 0; i--)
        {
            if (items[i].Depth == depth - 1)
            {
                return i;
            }
        }
        return -1;
    }

    // ── 파일 선택 (단일 · Ctrl 다중 · Shift 범위) — 코어(OrderedSet) 위임 ─────
    // 선택 상태는 DirItem.IsSelected(행 배경). 범위 기준점(anchor)은 패널별.

    /// <summary>포인터 원본이 속한 행의 <see cref="DirItem"/>(행 밖이면 null). 빈 영역 클릭 판정용.</summary>
    private static DirItem? RowUnderPointer(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is FrameworkElement fe && fe.Tag is DirItem d)
            {
                return d;
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>그리드 빈 영역(행 아님) 좌클릭 → 그 패널 선택 해제(빈 영역 클릭 = 선택 취소).</summary>
    private void OnGridPressed(object sender, PointerRoutedEventArgs e)
    {
        var pp = e.GetCurrentPoint((UIElement)sender).Properties;
        if (!pp.IsLeftButtonPressed)
        {
            return;   // 좌클릭만(우클릭=컨텍스트 메뉴)
        }
        if (RowUnderPointer(e.OriginalSource as DependencyObject) is not null)
        {
            return;   // 행 위 클릭은 행 핸들러가 처리
        }
        bool left = ReferenceEquals(sender, DirGrid);
        var items = Panel(left).Items;
        items.ClearSelection();
        items.SetCaret(-1);
        UpdateSelectionCount(items);
        CancelPendingRename();
    }

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // 마우스 뒤로/앞으로(XButton)·우클릭은 여기서 선택/이름변경 관여 금지
        // (우클릭=ContextRequested가 선택 판정, XButton=네비게이션). B-8: 우클릭·드래그 시 이름변경 오발동 방지.
        var pp = e.GetCurrentPoint((UIElement)sender).Properties;
        if (pp.IsXButton1Pressed || pp.IsXButton2Pressed || pp.IsRightButtonPressed)
        {
            return;
        }
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        if (item.IsRenaming)
        {
            return;   // 편집 중 — TextBox가 처리(간섭 금지)
        }
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        var items = Panel(left).Items;
        SetActivePanel(left);   // 클릭한 패널이 활성 → 반대 패널 선택은 회색(포커스아웃)
        Panel(left).Grid.Focus(FocusState.Programmatic);   // 키보드 이동 대상 포커스

        // 선택 규약: Shift=범위, Ctrl=토글(즉시). 평범한 클릭은 대상 상태에 따라:
        //  · 미선택 항목 → 즉시 단일 선택(+이름변경 후보 장전)
        //  · 이미 선택된 항목 → 선택을 바꾸지 않고 릴리스까지 보류(_deferredClickItem)
        //    → 다중 선택 드래그 보존(B-9m) + 이름변경은 드래그 없이 릴리스했을 때만(B-8).
        CancelPendingRename();   // 새 press → 대기 중인 이름변경 취소(더블클릭이면 DoubleTapped가 실행)
        _deferredClickItem = null;
        if (IsShiftDown())
        {
            items.SelectRange(item);
        }
        else if (IsCtrlDown())
        {
            items.Select(item, 1);   // 토글(다중)
        }
        else if (item.IsSelected)
        {
            _deferredClickItem = item;   // 보류(릴리스에서 확정)
        }
        else
        {
            items.Select(item, 0);       // 새 항목 → 즉시 단일
        }
        int idx = items.IndexOf(item);
        if (idx >= 0)
        {
            items.SetCaret(idx);
        }
        UpdateSelectionCount(items);
    }

    /// <summary>행 포인터 릴리스(드래그 없이 클릭 완료) — 보류된 선택/이름변경을 확정(B-8/B-9m).</summary>
    private void OnRowPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item
            || !ReferenceEquals(_deferredClickItem, item))
        {
            return;   // 보류된 항목의 릴리스만 처리(드래그였다면 릴리스가 여기로 안 옴)
        }
        _deferredClickItem = null;
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        var items = Panel(left).Items;

        if (items.SelectionCount == 1 && item.IsSelected)
        {
            // 이미 단독 선택된 항목의 클릭 → 이름변경 후보. 단, 곧 더블클릭(실행/진입)일 수 있으므로
            // 더블클릭 시간만큼 기다렸다가(그 사이 DoubleTapped가 오면 취소) 이름변경 시작(타이밍 버그 수정).
            ScheduleRename(fe, item, left);
            return;
        }

        // 다중 선택 중 한 항목을 클릭(드래그 아님) → 그 항목으로 단일 축소(Explorer식).
        items.Select(item, 0);
        int idx = items.IndexOf(item);
        if (idx >= 0)
        {
            items.SetCaret(idx);
        }
        UpdateSelectionCount(items);
    }

    // ── 이름변경 지연 트리거(더블클릭 실행과 구분) ─────────────────────
    private readonly DispatcherTimer _renameDelayTimer = new();
    private DirItem? _renamePendingItem;
    private FrameworkElement? _renamePendingRow;
    private bool _renamePendingLeft;

    /// <summary>더블클릭 시간 후 이름변경을 시작하도록 예약(그 사이 더블클릭이 오면 <see cref="CancelPendingRename"/>로 취소).</summary>
    private void ScheduleRename(FrameworkElement row, DirItem item, bool left)
    {
        _renamePendingRow = row;
        _renamePendingItem = item;
        _renamePendingLeft = left;
        _renameDelayTimer.Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime() + 60);   // 더블클릭 판정 여유
        _renameDelayTimer.Stop();
        _renameDelayTimer.Start();
    }

    /// <summary>대기 중인 이름변경 취소(더블클릭 실행/진입·새 클릭·드래그 시).</summary>
    private void CancelPendingRename()
    {
        _renameDelayTimer.Stop();
        _renamePendingItem = null;
        _renamePendingRow = null;
    }

    // 이미 선택된 항목을 평범하게 눌렀을 때 선택 변경을 릴리스까지 보류(다중 드래그 보존·이름변경 판정, B-8/B-9m).
    private DirItem? _deferredClickItem;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private DirItem? _renamingItem;   // 활성 인라인 편집 항목 — 편집기 밖 클릭(빈 영역 등) 커밋 판정용

    /// <summary>노드가 활성 이름변경 편집기(TextBox) 내부인가 — 밖이면 press 시 커밋 대상.</summary>
    private static bool IsWithinRenameEditor(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is TextBox tb && tb.Tag is DirItem d && d.IsRenaming)
            {
                return true;
            }
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    /// <summary>인라인 이름변경 시작 — 이름 셀을 편집기로 전환하고 포커스 + 이름부(확장자 제외) 선택.</summary>
    private void BeginRename(FrameworkElement row, DirItem item, bool left)
    {
        SetActivePanel(left);
        item.EditName = item.Name;
        item.IsRenaming = true;
        _renamingItem = item;
        // 편집기가 가시화된 뒤(같은 디스패치 후) 포커스·선택.
        row.DispatcherQueue.TryEnqueue(() =>
        {
            if (FindDescendant<TextBox>(row) is TextBox tb)
            {
                tb.Focus(FocusState.Programmatic);
                tb.Select(0, NameSelectLength(item));
            }
        });
    }

    /// <summary>편집기에서 Enter=커밋 · Esc=취소.</summary>
    private void OnRenameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not DirItem item)
        {
            return;
        }
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            item.EditName = tb.Text;
            CommitRename(item);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            CancelRename(item);
        }
    }

    /// <summary>편집기 포커스 상실(영역 밖 클릭) = 커밋(Enter와 동일).</summary>
    private void OnRenameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is DirItem item && item.IsRenaming)
        {
            item.EditName = tb.Text;
            CommitRename(item);
        }
    }

    /// <summary>편집 취소 — 원래 이름 복원, 목록에 포커스 반환.</summary>
    private void CancelRename(DirItem item)
    {
        _renamingItem = null;
        item.EditName = item.Name;
        item.IsRenaming = false;
        Panel(_activeLeft).Grid.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// 이름 변경 확정 — 디스크에서 rename 후 폴더 재로드 + 새 경로 재선택. 무변경/빈 이름은 취소와 동일.
    /// 잘못된 문자·중복·실패는 상태바로 격리(편집 종료). (후속: nexa-ops 경유·Undo·진행률.)
    /// </summary>
    private void CommitRename(DirItem item)
    {
        if (!item.IsRenaming)
        {
            return;
        }
        bool left = _activeLeft;   // 이름변경은 활성 패널에서 일어남
        _renamingItem = null;
        string newName = (item.EditName ?? string.Empty).Trim();
        item.IsRenaming = false;
        Panel(left).Grid.Focus(FocusState.Programmatic);
        if (string.IsNullOrEmpty(newName) || string.Equals(newName, item.Name, StringComparison.Ordinal))
        {
            return;   // 무변경/빈 이름 → 취소와 동일
        }
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            StatusText.Text = $"이름에 사용할 수 없는 문자가 있습니다: {newName}";
            return;
        }
        string? dir = Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        string newPath = Path.Combine(dir, newName);
        // 대소문자만 바꾸는 것(같은 파일)은 허용, 그 외 이미 존재하면 거부.
        if ((File.Exists(newPath) || Directory.Exists(newPath))
            && !string.Equals(newPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = $"같은 이름이 이미 있습니다: {newName}";
            return;
        }
        try
        {
            if (item.IsDir)
            {
                Directory.Move(item.FullPath, newPath);
            }
            else
            {
                File.Move(item.FullPath, newPath);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"이름 변경 실패: {ex.Message}";
            return;
        }
        _history.Push(new RenameOp(item.FullPath, newPath, $"이름 변경: {item.Name} → {newName}"));   // undo 기록(B-13u)
        StatusText.Text = $"이름 변경: {item.Name} → {newName}";
        var tab = Panel(left).Active;
        UpdateExpandedPaths(tab, item.FullPath, newPath);   // 펼침 경로(폴더/자식) 갱신
        tab.Loaded = false;
        LoadDirectory(left, tab, onLoaded: () => SelectByPath(left, newPath));   // 재로드 후 새 경로 재선택
    }

    /// <summary>이름변경으로 바뀐 경로를 탭 펼침셋에 반영(폴더 자신 + 그 하위 펼침 경로 접두사 치환).</summary>
    private static void UpdateExpandedPaths(PanelTab tab, string oldPath, string newPath)
    {
        string oldTrim = oldPath.TrimEnd('\\', '/');
        string newTrim = newPath.TrimEnd('\\', '/');
        var affected = tab.Expanded.Where(p =>
            p.Equals(oldTrim, StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(oldTrim + "\\", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var old in affected)
        {
            tab.Expanded.Remove(old);
            tab.Expanded.Add(newTrim + old[oldTrim.Length..]);
        }
    }

    /// <summary>비주얼 트리에서 첫 <typeparamref name="T"/> 자손을 찾는다(편집기 TextBox 포커스용).</summary>
    private static T? FindDescendant<T>(DependencyObject root) where T : class
    {
        int n = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (c is T hit)
            {
                return hit;
            }
            if (FindDescendant<T>(c) is T deep)
            {
                return deep;
            }
        }
        return null;
    }

    /// <summary>편집 시작 시 선택 길이 — 파일은 확장자 앞까지, 폴더/선행점 파일은 전체.</summary>
    private static int NameSelectLength(DirItem item)
    {
        string n = item.Name;
        if (item.IsDir)
        {
            return n.Length;
        }
        int dot = n.LastIndexOf('.');
        return dot > 0 ? dot : n.Length;
    }

    /// <summary>행 더블클릭 → 폴더/링크는 진입, 파일은 기본 연결 프로그램으로 실행(<see cref="ActivateItem"/>).</summary>
    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        CancelPendingRename();   // 더블클릭(진입/실행) → 대기 중 이름변경 취소
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        ActivateItem(left, item);   // 폴더=진입, 파일=연결 프로그램 실행
        e.Handled = true;
    }

    /// <summary>항목 활성화: 폴더/심볼릭링크는 진입(더블클릭 동작), 파일은 기본 연결 프로그램으로 실행한다.
    /// 파일 실행은 셸 실행(ShellExecute)을 사용 — StorageFile 브로커가 시스템/숨김 파일(desktop.ini 등)에
    /// 대해 "unauthorized"로 실패하던 문제를 피하고 더 많은 형식을 처리한다.</summary>
    private void ActivateItem(bool left, DirItem item)
    {
        if (item.IsDir || item.Kind == NexaFileKind.Symlink)
        {
            Navigate(left, item.FullPath, record: true);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FullPath)
            {
                UseShellExecute = true,   // 셸이 확장자 기본 프로그램으로 연다
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"실행 실패: {ex.Message}";
        }
    }

    // ── 컨텍스트 메뉴 · 파일 작업 (복사/잘라내기/붙여넣기/완전삭제) — FileOps/FileClipboard 위임 ─────

    /// <summary>행 우클릭 → <b>클래식 셸 컨텍스트 메뉴 + 고유 항목 병합</b>(ADR-0005, B-2 S1).
    /// 셸이 표준 동작(열기·잘라내기/복사·삭제·속성·확장 항목)을 제공하고, 고유 항목(잘라내기/복사는 앱
    /// 클립보드 연동 위해 자체, 완전 삭제·인라인 이름변경·폴더에 붙여넣기)을 병합. 클릭 항목이
    /// 현재 선택에 없으면 단일 선택으로 맞춘 뒤 표시(ContextTargets).</summary>
    private void OnRowContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        e.Handled = true;
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        SetActivePanel(left);

        // 탐색기 표준 선택 판정(ContextTargets): 클릭 항목이 현재 선택에 포함 → 선택 유지 + 선택 전체가 대상 /
        // 미포함 → 그 항목 단일 선택으로 교체 후 그 항목만 대상.
        var targets = ContextTargets(left, item);
        // 셸 메뉴는 같은 부모 폴더 항목만 전달 가능(ADR-0005 S1) — 교차 부모 선택이면 클릭 항목 폴더 기준으로 축소.
        string parent = ParentDir(item.FullPath);
        var sameDir = targets.Where(p => PathEq(ParentDir(p), parent)).ToList();
        if (sameDir.Count == 0)
        {
            sameDir.Add(item.FullPath);
        }

        // 고유 병합 항목(0x8000+): 셸이 제공하지 않거나(완전 삭제·폴더에 붙여넣기) 앱 통합이 나은 것(인라인 이름변경 —
        // 셸 rename 동사는 호스트 밖에선 무동작이라 CMF_CANRENAME 미사용, 우리 인라인 편집기 사용).
        var custom = new List<ShellContextMenu.CustomItem>();
        if (item.IsDir)
        {
            custom.Add(new(0x8001, "폴더에 붙여넣기", CanPaste(), () => PasteIntoDir(left, item.FullPath)));
        }
        custom.Add(new(0x8002, "이름 바꾸기(F2)", true, () => BeginRename(fe, item, left)));
        custom.Add(new(0x8003, "완전 삭제(Shift+Del)", true, () => DeletePaths(left, targets, permanent: true)));

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        bool shift = IsShiftDown();   // 키 상태는 클릭 시점 캡처
        // TrackPopupMenuEx는 UI 스레드를 모달로 점유 → 방금 바꾼 선택 하이라이트가 렌더된 "다음" 메뉴를 연다
        // (동기로 열면 단일 선택 교체가 화면에 안 보인 채 메뉴가 뜸).
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            var result = new ShellContextMenu().Show(hwnd, sameDir, custom, extendedVerbs: shift,
                // "삭제" 동사는 셸 대신 우리 삭제 경로로 — undo 기록(DeleteBatchOp, B-13u)·상태바·재로드 통합.
                verbInterceptor: verb =>
                {
                    if (string.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase))
                    {
                        DeletePaths(left, targets, permanent: false);   // 휴지통(Ctrl+Z 복원 가능)
                        return true;
                    }
                    return false;
                });
            if (result == ShellContextMenu.Result.ShellCommand)
            {
                // 셸 명령(붙여넣기·압축해제 등)은 우리 전송 엔진 밖에서 FS를 바꿈 → 지연 재로드(비동기 명령 여유).
                ScheduleShellRefresh();
            }
        });
    }

    /// <summary>셸 명령 실행 후 지연 패널 갱신 — 셸 동사는 비동기(확인창 등)라 약간 기다렸다 양쪽 재로드.
    /// (watcher 1차가 놓치는 케이스 보완. 근본은 B-12w.)</summary>
    private void ScheduleShellRefresh()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(800);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => ReloadBothPanels();
        timer.Start();
    }

    /// <summary>패널 빈 영역 우클릭(행이 소비 안 한 곳) → <b>기존 선택 해제</b>(탐색기 동일 — 빈 영역 우클릭=
    /// 선택 취소 후 배경 모드) + 빈영역 컨텍스트 메뉴(붙여넣기·새로고침). B-10c.</summary>
    private void OnPanelContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }
        bool left = ReferenceEquals(sender, DirGrid) ? true
            : ReferenceEquals(sender, DirGrid2) ? false
            : PanelUnderPointer(fe) ?? _activeLeft;
        SetActivePanel(left);

        // 빈 영역 우클릭 = 선택 취소(좌클릭과 동일 규약) — 배경 메뉴가 "현재 폴더" 대상임을 시각적으로 명확히.
        var panelItems = Panel(left).Items;
        panelItems.ClearSelection();
        panelItems.SetCaret(-1);
        UpdateSelectionCount(panelItems);

        var flyout = new MenuFlyout();
        var paste = new MenuFlyoutItem { Text = "붙여넣기", IsEnabled = CanPaste() };
        paste.Click += (_, _) => PasteInto(left);   // 현재 폴더로
        flyout.Items.Add(paste);

        // 실행 취소/다시 실행(B-13u) — 탐색기 배경 메뉴 동일. 마지막 작업 설명 표기, 없으면 비활성.
        var undo = new MenuFlyoutItem
        {
            Text = _history.CanUndo ? $"실행 취소: {_history.UndoDescription}" : "실행 취소",
            IsEnabled = _history.CanUndo,
            KeyboardAcceleratorTextOverride = "Ctrl+Z",
        };
        undo.Click += (_, _) => DoUndo();
        flyout.Items.Add(undo);
        var redo = new MenuFlyoutItem
        {
            Text = _history.CanRedo ? $"다시 실행: {_history.RedoDescription}" : "다시 실행",
            IsEnabled = _history.CanRedo,
            KeyboardAcceleratorTextOverride = "Ctrl+Y",
        };
        redo.Click += (_, _) => DoRedo();
        flyout.Items.Add(redo);
        flyout.Items.Add(new MenuFlyoutSeparator());

        // 새로 만들기 ▶ 폴더 / 파일 / 바로 가기 (BG-N1/N2/N3) — 생성 후 즉시 인라인 이름변경.
        var newSub = new MenuFlyoutSubItem { Text = "새로 만들기" };
        var newFolder = new MenuFlyoutItem { Text = "폴더" };
        newFolder.Click += (_, _) => CreateNewFolder(left);
        newSub.Items.Add(newFolder);
        var newFile = new MenuFlyoutItem { Text = "파일" };
        newFile.Click += (_, _) => CreateNewFile(left);
        newSub.Items.Add(newFile);
        var newLink = new MenuFlyoutItem { Text = "바로 가기" };
        newLink.Click += (_, _) => CreateNewShortcut(left);
        newSub.Items.Add(newLink);
        flyout.Items.Add(newSub);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var refresh = new MenuFlyoutItem { Text = "새로고침(F5)" };
        refresh.Click += (_, _) => ReloadPanel(left);
        flyout.Items.Add(refresh);

        if (e.TryGetPosition(fe, out var pos))
        {
            flyout.ShowAt(fe, new FlyoutShowOptions { Position = pos });
        }
        else
        {
            flyout.ShowAt(fe);
        }
        e.Handled = true;
    }

    // ── 새로 만들기 (빈영역 컨텍스트 · New submenu) — 폴더/파일/바로가기 (BG-N1/N2/N3) ─────

    /// <summary>현재 폴더에 새 폴더를 만들고(충돌 없는 이름) 즉시 인라인 이름변경을 시작한다.</summary>
    private void CreateNewFolder(bool left)
    {
        string dir = Panel(left).Active.Current;
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        string path = UniqueChildPath(dir, "새 폴더", "");
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"폴더 만들기 실패: {ex.Message}";
            return;
        }
        _history.Push(new CreateOp(path, "새 폴더 만들기", FileOps.DeleteToRecycleBin, () => Directory.CreateDirectory(path)));
        RevealAndRename(left, path);
    }

    /// <summary>현재 폴더에 새 빈 텍스트 파일을 만들고 즉시 인라인 이름변경을 시작한다.</summary>
    private void CreateNewFile(bool left)
    {
        string dir = Panel(left).Active.Current;
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        string path = UniqueChildPath(dir, "새 파일", ".txt");
        try
        {
            using (File.Create(path)) { }   // 빈 파일 생성 후 즉시 닫기
        }
        catch (Exception ex)
        {
            StatusText.Text = $"파일 만들기 실패: {ex.Message}";
            return;
        }
        _history.Push(new CreateOp(path, "새 파일 만들기", FileOps.DeleteToRecycleBin, () => { using (File.Create(path)) { } }));
        RevealAndRename(left, path);
    }

    /// <summary>대상 파일을 선택받아 현재 폴더에 바로 가기(.lnk)를 만들고 즉시 인라인 이름변경을 시작한다.
    /// (폴더 대상 바로 가기는 후속 — FileOpenPicker는 파일만 선택.)</summary>
    private async void CreateNewShortcut(bool left)
    {
        string dir = Panel(left).Active.Current;
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        string? target = await PickShortcutTargetAsync();
        if (string.IsNullOrEmpty(target))
        {
            return;   // 취소
        }
        string baseName = Path.GetFileNameWithoutExtension(target.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "새 바로 가기";
        }
        string path = UniqueChildPath(dir, $"{baseName} - 바로 가기", ".lnk");
        try
        {
            ShellLink.Create(path, target);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"바로 가기 만들기 실패: {ex.Message}";
            return;
        }
        _history.Push(new CreateOp(path, "바로 가기 만들기", FileOps.DeleteToRecycleBin, () => ShellLink.Create(path, target)));
        RevealAndRename(left, path);
    }

    /// <summary>바로 가기 대상 파일 선택 — FileOpenPicker(데스크톱은 창 핸들 초기화 필요). 취소 시 null.</summary>
    private async Task<string?> PickShortcutTargetAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary><paramref name="dir"/> 안에서 충돌하지 않는 경로("base"+ext, 있으면 " (2)", " (3)"…).</summary>
    private static string UniqueChildPath(string dir, string baseName, string ext)
    {
        string path = Path.Combine(dir, baseName + ext);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }
        for (int n = 2; ; n++)
        {
            path = Path.Combine(dir, $"{baseName} ({n}){ext}");
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return path;
            }
        }
    }

    /// <summary>새로 만든 항목을 현재 폴더 재로드 후 선택·스크롤로 드러내고 즉시 인라인 이름변경을 시작한다.
    /// 오프스크린 행은 <see cref="SelectByPath"/>가 강제 실체화하므로 다음 디스패치에 행 요소를 얻어 편집.</summary>
    private void RevealAndRename(bool left, string path)
    {
        var tab = Panel(left).Active;
        tab.Loaded = false;
        LoadDirectory(left, tab, onLoaded: () =>
        {
            SelectByPath(left, path);   // 선택+캐럿+스크롤(오프스크린 강제 실체화)
            var items = Panel(left).Items;
            int i = items.IndexOfPath(path);
            if (i < 0)
            {
                return;
            }
            var grid = Panel(left).Grid;
            grid.DispatcherQueue.TryEnqueue(() =>
            {
                if (grid.RowElement(i) is FrameworkElement row && items[i] is DirItem it)
                {
                    BeginRename(row, it, left);
                }
            });
        });
    }

    /// <summary>작업 대상 경로 목록 — 클릭 항목이 현재 선택에 포함되면 선택 전체, 아니면 클릭 항목 단독(단일 선택).</summary>
    private IReadOnlyList<string> ContextTargets(bool left, DirItem clicked)
    {
        var items = Panel(left).Active.Items;
        var sel = items.SelectedPaths();
        if (sel.Count > 0 && sel.Any(p => PathEq(p, clicked.FullPath)))
        {
            return sel;
        }
        items.Select(clicked, 0);   // 단일 선택으로 맞춤
        UpdateSelectionCount(items);
        return new[] { clicked.FullPath };
    }

    /// <summary>키보드 단축키(Ctrl+C/X/V·Del) 대상 — 현재 선택이 있으면 선택 전체, 없으면 캐럿 항목.</summary>
    private IReadOnlyList<string> KeyboardTargets(bool left)
    {
        var items = Panel(left).Active.Items;
        var sel = items.SelectedPaths();
        if (sel.Count > 0)
        {
            return sel;
        }
        return items.CaretItem is DirItem c ? new[] { c.FullPath } : System.Array.Empty<string>();
    }

    /// <summary>대상 경로를 앱 클립보드에 복사 표시(붙여넣기 = 복사).</summary>
    private void CopyPaths(bool left, IReadOnlyList<string> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }
        FileClipboard.SetCopy(targets);
        StatusText.Text = $"복사 {targets.Count}개";
    }

    /// <summary>대상 경로를 앱 클립보드에 잘라내기 표시(붙여넣기 = 이동).</summary>
    private void CutPaths(bool left, IReadOnlyList<string> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }
        FileClipboard.SetCut(targets);
        StatusText.Text = $"잘라내기 {targets.Count}개";
    }

    /// <summary>클립보드 내용을 지정 패널의 <b>현재 폴더</b>에 붙여넣는다(cut=이동·copy=복사).</summary>
    private void PasteInto(bool left) => PasteIntoDir(left, Panel(left).Active.Current);

    /// <summary>붙여넣을 내용이 있는가 — <b>앱 내부 클립보드</b> 또는 <b>OS 클립보드</b>(탐색기 등에서 복사한 파일).
    /// 컨텍스트 메뉴 "붙여넣기" 활성화 판정에 사용.</summary>
    private static bool CanPaste()
    {
        if (FileClipboard.HasContent)
        {
            return true;
        }
        try
        {
            return Clipboard.GetContent().Contains(StandardDataFormats.StorageItems);
        }
        catch
        {
            return false;   // 클립보드 접근 실패는 격리
        }
    }

    /// <summary>클립보드 내용을 <paramref name="destDir"/>에 붙여넣는다(폴더 우클릭=그 폴더, 빈영역=현재 폴더).
    /// 앱 내부 클립보드 우선, 없으면 <b>OS 클립보드</b>(탐색기 복사)에서 파일을 가져온다. 실제 전송은 DnD와
    /// <b>동일 경로</b>(<see cref="TransferPathsInto"/>) → 진행 창·덮어쓰기 확인·진행률·취소 공용.</summary>
    private async void PasteIntoDir(bool left, string destDir)
    {
        if (string.IsNullOrEmpty(destDir))
        {
            return;
        }
        // 1) 앱 내부 클립보드 우선.
        if (FileClipboard.HasContent)
        {
            bool cut = FileClipboard.IsCut;
            var paths = new List<string>(FileClipboard.Paths);   // 스냅샷(아래 Clear 대비)
            if (cut)
            {
                FileClipboard.Clear();   // 잘라내기 붙여넣기 = 1회성
            }
            TransferPathsInto(left, paths, destDir, cut ? DataPackageOperation.Move : DataPackageOperation.Copy);
            return;
        }
        // 2) OS 클립보드(탐색기 등에서 복사/잘라낸 파일).
        try
        {
            var view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }
            var items = await view.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (paths.Count == 0)
            {
                return;
            }
            bool cut = await OsClipboardIsCutAsync(view);
            TransferPathsInto(left, paths, destDir, cut ? DataPackageOperation.Move : DataPackageOperation.Copy);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"붙여넣기 실패: {ex.Message}";
        }
    }

    /// <summary>OS 클립보드의 "Preferred DropEffect"로 잘라내기(이동) 여부 판정 — 실패/없으면 복사로 간주.</summary>
    private static async Task<bool> OsClipboardIsCutAsync(Windows.ApplicationModel.DataTransfer.DataPackageView view)
    {
        try
        {
            if (!view.Contains("Preferred DropEffect"))
            {
                return false;
            }
            if (await view.GetDataAsync("Preferred DropEffect") is Windows.Storage.Streams.IRandomAccessStream ras)
            {
                var reader = new Windows.Storage.Streams.DataReader(ras.GetInputStreamAt(0))
                {
                    ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian,
                };
                await reader.LoadAsync(4);
                uint effect = reader.ReadUInt32();   // DROPEFFECT_COPY=1 · MOVE=2
                return (effect & 2) != 0;
            }
        }
        catch { /* 격리 → 복사 */ }
        return false;
    }

    /// <summary>대상 경로를 <b>완전 삭제</b>한다(휴지통 아님). 확인 대화상자 후 실행, 재로드.</summary>
    /// <summary>대상 삭제. <paramref name="permanent"/>=true면 완전삭제(확인 대화상자), false면 휴지통(되돌리기 가능).</summary>
    private async void DeletePaths(bool left, IReadOnlyList<string> targets, bool permanent)
    {
        if (targets.Count == 0)
        {
            return;
        }
        if (permanent)
        {
            var dialog = new ContentDialog
            {
                Title = "완전 삭제",
                Content = $"{targets.Count}개 항목을 완전히 삭제합니다.\n휴지통으로 가지 않으며 되돌릴 수 없습니다.",
                PrimaryButtonText = "삭제",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootGrid.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }
        int ok = 0;
        string? err = null;
        var deleted = new List<string>();   // 휴지통 삭제 성공분 — undo(복원) 기록(B-13u S2)
        foreach (var p in targets)
        {
            try
            {
                if (permanent) { FileOps.DeletePermanent(p); }
                else { FileOps.DeleteToRecycleBin(p); deleted.Add(p); }
                ok++;
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
        }
        // 휴지통 삭제만 undo 가능(완전 삭제는 설계상 제외 — 확인창으로 방어, docs/33).
        if (deleted.Count > 0)
        {
            _history.Push(new DeleteBatchOp(deleted, $"삭제(휴지통) {deleted.Count}개"));
        }
        string kind = permanent ? "완전 삭제" : "휴지통";
        StatusText.Text = err is null ? $"{kind} {ok}개 완료" : $"{kind} 일부 실패: {err}";
        ReloadBothPanels();   // 양쪽 갱신(BUG-006 임시 — watcher 전까지)
    }

    /// <summary>지정 패널을 현재 경로로 재로드한다(파일 작업 후 반영). 펼침 상태 유지.</summary>
    private void ReloadPanel(bool left)
    {
        var tab = Panel(left).Active;
        if (string.IsNullOrEmpty(tab.Current))
        {
            return;
        }
        tab.Loaded = false;
        LoadDirectory(left, tab);
    }

    /// <summary>끝 구분자·대소문자 무시 경로 비교.</summary>
    private static bool PathEq(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    // ── 드래그 앤 드롭 (패널 내/좌우 폴더 이동) — B-11/B-12 ─────────────

    private List<string> _dragPaths = new();   // 현재 드래그 중인 원본 경로들(앱 내부 DnD)
    private bool _dragSourceLeft;              // 드래그 시작 패널

    /// <summary>행 드래그 시작 → 대상(선택 또는 드래그 항목)을 앱 내부 드래그 상태에 담고 외부 드롭용 데이터를 구성한다.
    /// <para><b>기본(탐색기 동일)</b>: 실제 파일/폴더 항목(<c>StorageItems</c>)을 넣어 대상 앱이 <b>파일을 연다</b>(Sublime 등).
    /// <b>Alt</b>를 누른 채 드래그하면 경로 <b>텍스트</b>만 넣어 대상에 경로가 붙여넣기된다(사용자 요청).</para>
    /// (앱 내부 이동/복사는 <c>_dragPaths</c>로 처리 — 데이터 패키지와 무관하므로 위 분기와 독립.)</summary>
    private async void OnRowDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            args.Cancel = true;
            return;
        }
        _deferredClickItem = null;   // 드래그 시작 → 보류된 클릭 취소(선택 유지, B-9m)
        CancelPendingRename();       // 드래그 중엔 대기 중 이름변경 취소(오발동 방지, B-8)
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        var items = Panel(left).Active.Items;
        var sel = items.SelectedPaths();
        _dragPaths = sel.Count > 0 && sel.Any(p => PathEq(p, item.FullPath))
            ? new List<string>(sel)
            : new List<string> { item.FullPath };
        _dragSourceLeft = left;
        // 이동·복사 둘 다 허용해야 대상이 Ctrl=복사/Shift=이동을 수락할 수 있음(둘 중 하나만이면 나머지는 금지=None).
        args.Data.RequestedOperation = DataPackageOperation.Move | DataPackageOperation.Copy;

        // Alt: 경로 텍스트만 → 대상에 경로 붙여넣기.
        if (IsAltDown())
        {
            args.Data.SetText(string.Join("\r\n", _dragPaths));
            return;
        }

        // 기본: 실제 파일/폴더 항목을 넣어 대상이 파일을 열게 한다(탐색기 동일). StorageItem 취득은
        // 비동기(브로커) → 드래그 시작을 지연(GetDeferral)해 완료 후 진행.
        // 항목당 브로커 왕복이라 순차 처리는 대량 선택에서 시작이 수 초 지연 → 병렬(WhenAll) 취득(P2).
        var deferral = args.GetDeferral();
        try
        {
            var fetched = await Task.WhenAll(_dragPaths.Select(async p =>
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        return (IStorageItem)await StorageFolder.GetFolderFromPathAsync(p);
                    }
                    if (File.Exists(p))
                    {
                        return (IStorageItem)await StorageFile.GetFileFromPathAsync(p);
                    }
                }
                catch
                {
                    // 개별 항목 실패(시스템/보호 파일 등)는 격리 → 아래 텍스트 폴백으로 커버.
                }
                return null;
            }));
            var storageItems = fetched.Where(it => it is not null).Cast<IStorageItem>().ToList();
            if (storageItems.Count > 0)
            {
                args.Data.SetStorageItems(storageItems, readOnly: false);
            }
            // 텍스트 폴백: 파일을 못 받는 텍스트 전용 대상엔 경로가 들어간다(탐색기도 텍스트 병행 제공).
            args.Data.SetText(string.Join("\r\n", _dragPaths));
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>드래그 종료(드롭 성공 또는 <b>ESC 취소</b>) — 상태 정리. 취소(결과 None)면 아무것도 이동 안 하고 알림(B-14).</summary>
    private void OnRowDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        _tabDwellTimer.Stop();
        _tabDwellTarget = null;
        CancelFolderDwell();
        DirGrid.StopDragAutoScroll();
        DirGrid2.StopDragAutoScroll();
        if (args.DropResult == DataPackageOperation.None && _dragPaths.Count > 0)
        {
            StatusText.Text = "드래그 취소 — 변경 없음";   // ESC 취소 또는 대상이 연산 미보고 → 파일 변화 없음
        }
        else if (args.DropResult.HasFlag(DataPackageOperation.Move) && _dragPaths.Count > 0)
        {
            // _dragPaths가 남아 있음 = 우리 드롭 핸들러 미경유 = 외부 대상(탐색기 등)이 이동을 수행 →
            // 원본이 사라졌으므로 패널 갱신(P2 — watcher 1차의 누락 보완, 내부 전송은 TransferPathsInto가 갱신).
            ReloadBothPanels();
        }
        _dragPaths.Clear();
    }

    /// <summary>폴더 행 위 드래그 → 수락(캡션 표시). 내부=볼륨/수정키 규칙(자기·하위 폴더 금지), 외부=파일 드래그면 복사 기본(DND-EXT).</summary>
    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        var op = DataPackageOperation.None;
        if (sender is FrameworkElement fe && fe.Tag is DirItem item && item.IsDir)
        {
            op = _dragPaths.Count > 0
                ? DragOp(item.FullPath, e.Modifiers)              // 내부: 같은 디스크=이동/다른=복사, Ctrl/Shift 강제(B-14dnd) + 자기/하위 금지
                : ExternalDragOp(e);                              // 외부(탐색기 등): StorageItems면 복사 기본
            e.AcceptedOperation = op;
            ApplyDragCaption(e.DragUIOverride, op, FolderLabel(item.FullPath));  // 탐색기식 라이브 캡션(SHELL-DND)
            // 이 폴더에 일정 시간 머물면 진입(spring-load, B-15h) — 수락된 대상에서만.
            if (op != DataPackageOperation.None && !ReferenceEquals(_folderDwellTarget, item))
            {
                _folderDwellTarget = item;
                _folderDwellLeft = PanelUnderPointer(fe) ?? _activeLeft;
                _folderDwellTimer.Stop();
                _folderDwellTimer.Start();
            }
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
            ApplyDragCaption(e.DragUIOverride, DataPackageOperation.None, null);   // 금지 대상 → 캡션 숨김
        }
        if (op == DataPackageOperation.None)
        {
            CancelFolderDwell();
        }
    }

    /// <summary>드래그가 폴더 행에서 벗어나면 spring-load dwell 취소(B-15h).</summary>
    private void OnRowDragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DirItem item && ReferenceEquals(_folderDwellTarget, item))
        {
            CancelFolderDwell();
        }
    }

    /// <summary>폴더 행에 드롭 → 그 폴더로 이동/복사(내부) 또는 외부 파일 드롭 수신(DND-EXT). 파일/빈 영역은 본문으로 버블(현재 폴더로, B-12).</summary>
    private async void OnRowDrop(object sender, DragEventArgs e)
    {
        CancelFolderDwell();
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item || !item.IsDir)
        {
            return;   // 파일 행 등 → 본문으로 버블(현재 폴더 드롭)
        }
        if (_dragPaths.Count > 0)
        {
            var op = DragOp(item.FullPath, e.Modifiers);
            if (op != DataPackageOperation.None)   // 자기/하위 폴더 등 금지면 무동작(방어 — DragOver가 이미 차단)
            {
                TransferPathsInto(_dragSourceLeft, _dragPaths, item.FullPath, op);
            }
            _dragPaths.Clear();
            e.Handled = true;   // 폴더 드롭만 소비
            return;
        }
        // 외부(탐색기/다른 인스턴스) 드롭: StorageItems에서 경로 추출(비동기 → deferral) 후 동일 엔진으로.
        var extOp = ExternalDragOp(e);
        if (extOp != DataPackageOperation.None)
        {
            e.Handled = true;
            var deferral = e.GetDeferral();
            try
            {
                var paths = await GetExternalPathsAsync(e.DataView);
                if (paths.Count > 0)
                {
                    TransferPathsInto(_activeLeft, paths, item.FullPath, extOp);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }
    }

    /// <summary>패널 빈 영역(행이 소비하지 않은 곳)에 드롭 → 그 패널의 현재 폴더로 이동/복사(좌우 겸용, B-12/B-14dnd).
    /// 내부 드래그는 <c>_dragPaths</c>, 외부(탐색기 등)는 <c>DataView</c>의 StorageItems에서 경로 추출(DND-EXT).</summary>
    private async void OnPanelBackgroundDrop(bool destLeft, DragEventArgs e)
    {
        string destDir = Panel(destLeft).Active.Current;
        if (_dragPaths.Count > 0)
        {
            var op = BackgroundDragOp(destLeft, e);
            if (op != DataPackageOperation.None && !string.IsNullOrEmpty(destDir))
            {
                TransferPathsInto(_dragSourceLeft, _dragPaths, destDir, op);   // 자기폴더 Move는 None → 무시
            }
            _dragPaths.Clear();
            return;
        }
        // 외부(탐색기/다른 인스턴스) 드롭: StorageItems 경로 추출(비동기 → deferral) 후 동일 엔진으로.
        var extOp = ExternalDragOp(e);
        if (extOp == DataPackageOperation.None || string.IsNullOrEmpty(destDir))
        {
            return;
        }
        var deferral = e.GetDeferral();
        try
        {
            var paths = await GetExternalPathsAsync(e.DataView);
            if (paths.Count > 0)
            {
                TransferPathsInto(destLeft, paths, destDir, extOp);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>
    /// 빈 영역(현재 폴더) 드롭의 연산 — 기본 <see cref="DragOp"/>에 <b>자기 폴더 규칙</b> 추가:
    /// 드래그 항목이 이미 이 폴더 소속인데 <b>Move면 무의미 → None(금지)</b>, <b>Copy는 복제 허용</b>(…(2)).
    /// (탐색기 동일: 같은 폴더로 그냥 끌면 아무 일 없음, Ctrl+끌면 사본 생성.)
    /// 외부(파일) 드래그는 <see cref="ExternalDragOp"/>(복사 기본), 파일 아닌 외부 드래그(텍스트 등)는 금지.
    /// </summary>
    private DataPackageOperation BackgroundDragOp(bool destLeft, DragEventArgs e)
    {
        string destDir = Panel(destLeft).Active.Current;
        if (string.IsNullOrEmpty(destDir))
        {
            return DataPackageOperation.None;
        }
        if (_dragPaths.Count == 0)
        {
            return ExternalDragOp(e);   // 외부: 파일 드래그(StorageItems)만 수락 — 예전 "수락 표시 후 무동작" 방지
        }
        var op = DragOp(destDir, e.Modifiers);
        if (op == DataPackageOperation.Move
            && _dragPaths.All(p => PathEq(ParentDir(p), destDir)))
        {
            return DataPackageOperation.None;   // 자기 폴더로 이동 = no-op → 금지
        }
        return op;
    }

    /// <summary>경로의 부모 디렉터리(끝 구분자 제거 후). 실패 시 빈 문자열.</summary>
    private static string ParentDir(string path)
    {
        try { return System.IO.Path.GetDirectoryName(path.TrimEnd('\\', '/')) ?? ""; }
        catch { return ""; }
    }

    // ── 드래그 중 탭 hover 전환 (2초 dwell) — B-13 ───────────────────

    private readonly DispatcherTimer _tabDwellTimer = new();
    private PanelTab? _tabDwellTarget;   // 현재 드래그가 머무는 탭

    // 드래그 중 폴더 위 dwell 진입(spring-load, B-15h).
    private readonly DispatcherTimer _folderDwellTimer = new();
    private DirItem? _folderDwellTarget;
    private bool _folderDwellLeft;

    private void CancelFolderDwell()
    {
        _folderDwellTimer.Stop();
        _folderDwellTarget = null;
    }

    /// <summary>드래그가 탭 위로 들어오면 2초 타이머 시작(다른 탭이면 재시작). 이동/복사 수락(내부=디스크·수정키, 외부=복사 기본).</summary>
    private void OnTabDragOver(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PanelTab tab && !string.IsNullOrEmpty(tab.Current))
        {
            var op = _dragPaths.Count > 0 ? DragOp(tab.Current, e.Modifiers) : ExternalDragOp(e);
            e.AcceptedOperation = op;
            ApplyDragCaption(e.DragUIOverride, op, FolderLabel(tab.Current));   // 탐색기식 라이브 캡션(SHELL-DND)
            if (op != DataPackageOperation.None && !ReferenceEquals(_tabDwellTarget, tab))
            {
                _tabDwellTarget = tab;
                _tabDwellTimer.Stop();
                _tabDwellTimer.Start();   // 이 탭에 2초 머물면 전환(Tick에서 SwitchToTab)
            }
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
            ApplyDragCaption(e.DragUIOverride, DataPackageOperation.None, null);
        }
    }

    /// <summary>탭에서 드래그가 벗어나면 dwell 타이머 취소.</summary>
    private void OnTabDragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && ReferenceEquals(fe.Tag, _tabDwellTarget))
        {
            _tabDwellTimer.Stop();
            _tabDwellTarget = null;
        }
    }

    /// <summary>탭에 드롭 → 그 탭의 폴더로 이동/복사(즉시, 2초 기다림 없이도 가능). 외부 파일 드롭도 수신(DND-EXT).</summary>
    private async void OnTabDrop(object sender, DragEventArgs e)
    {
        _tabDwellTimer.Stop();
        _tabDwellTarget = null;
        e.Handled = true;
        if (sender is not FrameworkElement fe || fe.Tag is not PanelTab tab || string.IsNullOrEmpty(tab.Current))
        {
            _dragPaths.Clear();
            return;
        }
        if (_dragPaths.Count > 0)
        {
            var op = DragOp(tab.Current, e.Modifiers);
            if (op != DataPackageOperation.None)   // 자기/하위 폴더 금지 방어
            {
                TransferPathsInto(_dragSourceLeft, _dragPaths, tab.Current, op);
            }
            _dragPaths.Clear();
            return;
        }
        var extOp = ExternalDragOp(e);
        if (extOp == DataPackageOperation.None)
        {
            return;
        }
        var deferral = e.GetDeferral();
        try
        {
            var paths = await GetExternalPathsAsync(e.DataView);
            if (paths.Count > 0)
            {
                TransferPathsInto(_activeLeft, paths, tab.Current, extOp);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>
    /// 드롭 연산 결정(B-14dnd) — Windows 표준 수정키:
    /// <b>Ctrl=복사 강제 · Shift=이동 강제</b>, 없으면 기본(<b>같은 볼륨=이동 / 다른 볼륨=복사</b>).
    /// (Alt는 OS 메뉴 활성화에 가로채여 신뢰 불가 → 표준 Ctrl/Shift 채택.)
    /// </summary>
    /// <summary>
    /// 드래그 UI를 <b>탐색기 방식</b>으로 — WinUI 기본 큰 글리프("↗ Move")는 숨기고, 연산·대상 폴더명을
    /// 시스템 폰트 <b>라이브 캡션</b>("…에 복사"/"…(으)로 이동")으로 표시. Ctrl/Shift 변경 시 DragOver가
    /// 다시 발생하며 캡션이 갱신된다(SHELL-DND, docs/33). 항목 고스트(IsContentVisible)는 유지.
    /// </summary>
    private static void ApplyDragCaption(Microsoft.UI.Xaml.DragUIOverride ui, DataPackageOperation op, string? destName)
    {
        ui.IsGlyphVisible = false;   // 탐색기엔 없는 큰 글리프 제거(예전 "Move 글자 큼" 원인)
        if (op == DataPackageOperation.None)
        {
            ui.IsCaptionVisible = false;   // 금지 대상 → 캡션 없음(불가 커서만)
            return;
        }
        ui.IsContentVisible = true;   // 드래그한 행 고스트 유지
        ui.IsCaptionVisible = true;
        bool copy = op.HasFlag(DataPackageOperation.Copy) && !op.HasFlag(DataPackageOperation.Move);
        string label = string.IsNullOrEmpty(destName) ? "" : destName;
        string caption = copy
            ? (label.Length == 0 ? "복사" : $"{label}에 복사")
            : (label.Length == 0 ? "이동" : $"{label}(으)로 이동");
        if (ui.Caption != caption)
        {
            ui.Caption = caption;   // 변경 시만 설정 — DragOver는 마우스 이동마다 오므로 동일 값 재설정(비주얼 갱신) 회피
        }
    }

    /// <summary>드롭 대상 폴더의 표시 이름(캡션용) — 리프 폴더명, 드라이브 루트면 경로 자체.</summary>
    private static string FolderLabel(string dir)
    {
        if (string.IsNullOrEmpty(dir))
        {
            return "";
        }
        string leaf = System.IO.Path.GetFileName(dir.TrimEnd('\\', '/'));
        return leaf.Length > 0 ? leaf : dir;   // "C:\\" 등 루트는 경로 그대로
    }

    private DataPackageOperation DragOp(string destDir, DragDropModifiers mods)
    {
        if (DragIntoSelfOrChild(destDir))
        {
            return DataPackageOperation.None;   // 자기 자신/하위 폴더로는 이동·복사 모두 금지(탐색기 동일, 순환 방지)
        }
        if (mods.HasFlag(DragDropModifiers.Control))
        {
            return DataPackageOperation.Copy;   // Ctrl = 복사 강제
        }
        if (mods.HasFlag(DragDropModifiers.Shift))
        {
            return DataPackageOperation.Move;   // Shift = 이동 강제
        }
        bool sameVol = _dragPaths.Count == 0 || VolumeEq(_dragPaths[0], destDir);
        return sameVol ? DataPackageOperation.Move : DataPackageOperation.Copy;   // 기본: 디스크별
    }

    /// <summary>드롭 대상이 드래그 원본(폴더) <b>자신 또는 그 하위</b>인가 — UI 단계에서 금지(커서/캡션).
    /// (기존엔 전송 단계 FileOps 예외로만 걸려 "일부 실패"로 끝났음 — 탐색기는 드래그 중 금지 커서.)</summary>
    private bool DragIntoSelfOrChild(string destDir)
    {
        string dest = destDir.TrimEnd('\\', '/');
        foreach (var p in _dragPaths)
        {
            string src = p.TrimEnd('\\', '/');
            if (dest.Equals(src, StringComparison.OrdinalIgnoreCase)
                || dest.StartsWith(src + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>외부(다른 앱/탐색기/다른 인스턴스) 드래그의 연산 — <b>파일 항목(StorageItems)일 때만</b> 수락.
    /// 원본 볼륨을 (동기) DragOver 중 알 수 없어 기본 <b>복사</b>(안전·비파괴), Shift=이동 강제(DND-EXT).</summary>
    private static DataPackageOperation ExternalDragOp(DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return DataPackageOperation.None;   // 텍스트 등 파일 아닌 드래그는 금지
        }
        return e.Modifiers.HasFlag(DragDropModifiers.Shift)
            ? DataPackageOperation.Move
            : DataPackageOperation.Copy;
    }

    /// <summary>외부 드롭의 DataView에서 파일시스템 경로 목록 추출. 경로 없는 가상 항목(zip 내부 등)은 제외.</summary>
    private static async Task<List<string>> GetExternalPathsAsync(Windows.ApplicationModel.DataTransfer.DataPackageView view)
    {
        var paths = new List<string>();
        try
        {
            var items = await view.GetStorageItemsAsync();
            foreach (var it in items)
            {
                if (!string.IsNullOrEmpty(it.Path))
                {
                    paths.Add(it.Path);
                }
            }
        }
        catch
        {
            // 원본 앱이 이미 닫혔거나 마샬링 실패 등 → 빈 목록(무동작)
        }
        return paths;
    }

    // ── 상승(관리자) 프로세스 OLE 드롭 폴백 (BUG-009) ──────────────────
    // WinUI 3는 상승 프로세스의 인바운드 드래그를 플랫폼에서 거부(XAML DragOver 미도달) —
    // UAC OFF PC는 항상 상승이라 탐색기→앱 드래그가 금지 커서가 된다. 고전 OLE IDropTarget으로
    // 우회하되, 판정 의미(DragOp/ExternalDragOp)와 전송 엔진(TransferPathsInto)은 XAML 경로와 동일.

    private OleDropTarget? _oleDropFallback;

    /// <summary>프로세스가 상승(관리자) 토큰인가 — UAC OFF PC는 탐색기에서 실행해도 상승.</summary>
    private static bool IsProcessElevated()
    {
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>상승 실행이면 고전 OLE 드롭 타깃 등록(비상승은 XAML 경로 정상 → 미등록으로 이중 처리 방지).</summary>
    private void InitElevatedDropFallback()
    {
        if (_oleDropFallback is not null || !IsProcessElevated())
        {
            return;
        }
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _oleDropFallback = OleDropTarget.TryRegister(hwnd);
        if (_oleDropFallback is not null)
        {
            _oleDropFallback.OverHandler = (x, y, keys, hasFiles) => OleEffect(OleDropPlan(x, y, keys, hasFiles).op);
            _oleDropFallback.DropHandler = OnOleDrop;
        }
    }

    /// <summary>OLE 폴백의 드롭 계획 — 화면(px) 좌표를 XAML(DIP)로 역변환·히트테스트해 대상을 정한다.
    /// 폴더 행 위면 그 폴더, 아니면 포인터 아래 패널의 현재 폴더(패널 밖이면 금지).
    /// 연산 규칙은 XAML 경로와 동일: 내부=<see cref="DragOp"/>(+자기 폴더 Move 금지), 외부=복사 기본·Shift=이동.</summary>
    private (DataPackageOperation op, bool destLeft, string destDir) OleDropPlan(int screenX, int screenY, uint keyState, bool hasFiles)
    {
        bool internalDrag = _dragPaths.Count > 0;
        if (!internalDrag && !hasFiles)
        {
            return (DataPackageOperation.None, true, "");   // 외부인데 파일(CF_HDROP) 아님 → 금지
        }
        // 화면 px → 창 클라이언트 px → XAML DIP.
        var pt = new POINT { X = screenX, Y = screenY };
        ScreenToClient(WinRT.Interop.WindowNative.GetWindowHandle(this), ref pt);
        double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        var xamlPt = new Windows.Foundation.Point(pt.X / scale, pt.Y / scale);
        // 히트테스트: 최상위(가장 깊은) 요소에서 폴더 행(Tag=DirItem)과 소속 패널 판정.
        DirItem? row = null;
        UIElement? topmost = null;
        foreach (var el in Microsoft.UI.Xaml.Media.VisualTreeHelper.FindElementsInHostCoordinates(xamlPt, RootGrid))
        {
            topmost ??= el;
            if (el is FrameworkElement fe && fe.Tag is DirItem d)
            {
                row = d;
                break;
            }
        }
        bool? left = PanelUnderPointer(topmost);
        bool destLeft;
        string destDir;
        if (row is { IsDir: true })
        {
            destLeft = left ?? _activeLeft;
            destDir = row.FullPath;
        }
        else if (left is bool l && !string.IsNullOrEmpty(Panel(l).Active.Current))
        {
            destLeft = l;
            destDir = Panel(l).Active.Current;
        }
        else
        {
            return (DataPackageOperation.None, true, "");   // 패널 밖(터미널·경로 바 등) → 금지
        }
        var mods = OleMods(keyState);
        DataPackageOperation op;
        if (internalDrag)
        {
            op = DragOp(destDir, mods);   // 같은 디스크=이동/다른=복사 + Ctrl/Shift 강제 + 자기/하위 금지
            if (op == DataPackageOperation.Move && row is not { IsDir: true }
                && _dragPaths.All(p => PathEq(ParentDir(p), destDir)))
            {
                op = DataPackageOperation.None;   // 빈 영역: 자기 폴더로 이동 = no-op(BackgroundDragOp 동일)
            }
        }
        else
        {
            // 외부(탐색기 등): 복사 기본·Shift=이동(ExternalDragOp 동일). 자기/하위 순환은 전송 엔진이 방어.
            op = mods.HasFlag(DragDropModifiers.Shift) ? DataPackageOperation.Move : DataPackageOperation.Copy;
        }
        return (op, destLeft, destDir);
    }

    /// <summary>OLE 폴백 드롭 — 내부 드래그는 <c>_dragPaths</c>, 외부는 CF_HDROP 경로로 동일 전송 엔진 실행.
    /// 이동도 앱이 직접 수행(최적화 이동)하므로 원본(탐색기)이 중복 삭제하지 않게 None을 보고한다.</summary>
    private uint OnOleDrop(int screenX, int screenY, uint keyState, List<string> paths)
    {
        bool internalDrag = _dragPaths.Count > 0;
        var (op, destLeft, destDir) = OleDropPlan(screenX, screenY, keyState, paths.Count > 0);
        if (op == DataPackageOperation.None)
        {
            return OleDropTarget.EffectNone;   // 내부 드래그면 DropCompleted가 "취소" 처리(_dragPaths 유지)
        }
        if (internalDrag)
        {
            TransferPathsInto(_dragSourceLeft, _dragPaths, destDir, op);
            _dragPaths.Clear();
            return OleDropTarget.EffectNone;   // 앱이 수행 완료 — 원본 측 후처리 불필요
        }
        if (paths.Count == 0)
        {
            return OleDropTarget.EffectNone;
        }
        TransferPathsInto(destLeft, paths, destDir, op);
        // 복사는 그대로 보고, 이동은 앱이 수행하므로 None(원본이 지우면 이중 삭제).
        return op == DataPackageOperation.Copy ? OleDropTarget.EffectCopy : OleDropTarget.EffectNone;
    }

    /// <summary>OLE MK_* 키 상태 → XAML <see cref="DragDropModifiers"/>(판정 로직 공용화용).</summary>
    private static DragDropModifiers OleMods(uint keyState)
    {
        var mods = DragDropModifiers.None;
        if ((keyState & 0x08) != 0) { mods |= DragDropModifiers.Control; }   // MK_CONTROL
        if ((keyState & 0x04) != 0) { mods |= DragDropModifiers.Shift; }     // MK_SHIFT
        return mods;
    }

    /// <summary>연산 → DROPEFFECT(커서 표시용).</summary>
    private static uint OleEffect(DataPackageOperation op) => op switch
    {
        DataPackageOperation.Copy => OleDropTarget.EffectCopy,
        DataPackageOperation.Move => OleDropTarget.EffectMove,
        _ => OleDropTarget.EffectNone,
    };

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hwnd, ref POINT pt);

    /// <summary>두 경로가 같은 볼륨(드라이브 루트)인가. 판단 실패 시 true(같은 디스크=이동 취급, 보수적).</summary>
    private static bool VolumeEq(string a, string b)
    {
        try
        {
            string ra = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(a.TrimEnd('\\', '/'))) ?? string.Empty;
            string rb = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(b.TrimEnd('\\', '/'))) ?? string.Empty;
            return string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// <paramref name="paths"/>를 <paramref name="destDir"/>로 <paramref name="op"/>(이동/복사)한다. 규칙(DND-OW):
    /// <list type="bullet">
    ///   <item><b>같은 폴더</b>: 이동=무동작 · 복사=순번 복제(" (2)"…).</item>
    ///   <item><b>다른 폴더</b> & 이름 충돌: 파일별로 <b>덮어쓰기 확인</b>(예=덮어씀 / 아니오=그 항목만 건너뜀).</item>
    /// </list>
    /// 충돌 없는 항목은 즉시 처리하고, 충돌 항목만 순차 확인한다. 진행률을 상태바에 표시(DND-OW2).
    /// (paths는 호출자가 곧 <c>_dragPaths</c>를 비우므로 <b>첫 await 전에 복사</b>해 안전.)
    /// </summary>
    // ── Undo/Redo (B-13u, docs/33) — 파일 작업 히스토리(세션 한정, 배치=1 트랜잭션) ─────
    private readonly OperationHistory _history = new();

    /// <summary>Ctrl+Z — 마지막 파일 작업 되돌리기. 실패는 상태바 알림(무결성 우선 — 강제/재시도 안 함).</summary>
    private void DoUndo()
    {
        if (!_history.CanUndo)
        {
            StatusText.Text = "되돌릴 작업이 없습니다";
            return;
        }
        string desc = _history.UndoDescription!;
        try
        {
            _history.Undo();
            StatusText.Text = $"실행 취소: {desc}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"실행 취소 실패({desc}): {ex.Message}";
        }
        ReloadBothPanels();
    }

    /// <summary>Ctrl+Y — 마지막 되돌리기를 다시 실행.</summary>
    private void DoRedo()
    {
        if (!_history.CanRedo)
        {
            StatusText.Text = "다시 실행할 작업이 없습니다";
            return;
        }
        string desc = _history.RedoDescription!;
        try
        {
            _history.Redo();
            StatusText.Text = $"다시 실행: {desc}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"다시 실행 실패({desc}): {ex.Message}";
        }
        ReloadBothPanels();
    }

    private async void TransferPathsInto(bool sourceLeft, IReadOnlyList<string> paths, string destDir, DataPackageOperation op)
    {
        bool copy = op == DataPackageOperation.Copy;
        var items = new List<string>(paths);   // 스냅샷(비동기 중 원본 clear 방지)
        string verb = copy ? "복사" : "이동";
        var performed = new List<(string Src, string Dest)>();   // 실제 수행 (원본, 실제 대상) — undo 기록(B-13u)
        int done = 0, skipped = 0;
        string? err = null;
        bool cancelled = false;
        bool overwriteAll = false;   // "모두 예" 선택 후로는 이후 충돌을 묻지 않고 덮어씀

        // 진행 창(별도 Window) — 시작 시 표시, 완료 후 기본은 열린 채 유지(자동 닫기 off).
        var win = new TransferProgressWindow(verb);
        win.ActivateForeground();   // 맨 앞으로 + 포커스
        win.SetPreparing();
        var ct = win.Token;

        long total = await Task.Run(() =>
        {
            long s = 0;
            foreach (var it in items) { s += FileOps.SizeOf(it); }
            return s;
        });
        win.SetDeterminate(total);

        long copied = 0;
        int fileNo = 0;
        string currentName = string.Empty;
        // 진행 보고(백그라운드 → UI 스레드로 마샬). 증분 바이트 누적 → 진행 창 갱신.
        var progress = new Progress<long>(delta =>
        {
            copied += delta;
            win.Report(copied, total, fileNo, items.Count, currentName);
        });
        void OnBytes(long b) => ((IProgress<long>)progress).Report(b);

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ct.IsCancellationRequested) { cancelled = true; break; }
                fileNo = i + 1;
                string p = items[i];
                currentName = FileOps.LeafName(p);
                win.Report(copied, total, fileNo, items.Count, currentName);
                try
                {
                    string? parent = System.IO.Path.GetDirectoryName(p.TrimEnd('\\', '/'));
                    bool sameFolder = parent is not null && PathEq(parent, destDir);
                    if (sameFolder)
                    {
                        if (copy)
                        {
                            // 같은 폴더 복사 = 순번 복제(진행 보고). I/O는 백그라운드.
                            string dup = FileOps.UniqueDest(destDir, FileOps.LeafName(p), Directory.Exists(p));
                            await Task.Run(() => FileOps.CopyOntoWithProgress(p, dup, overwrite: false, OnBytes, ct));
                            performed.Add((p, dup));
                            done++;
                        }
                        // 같은 폴더 이동 = 무동작
                        continue;
                    }
                    if (FileOps.Conflicts(destDir, p))
                    {
                        var choice = overwriteAll
                            ? OverwriteChoice.Yes
                            : await win.AskOverwriteAsync(FileOps.LeafName(p), copy);
                        if (choice == OverwriteChoice.Cancel)
                        {
                            cancelled = true;   // 전체 전송 중단
                            break;
                        }
                        if (choice == OverwriteChoice.No)
                        {
                            skipped++;
                            OnBytes(FileOps.SizeOf(p));   // 건너뛴 항목만큼 진행 전진(막대가 끝까지)
                            continue;
                        }
                        if (choice == OverwriteChoice.YesToAll)
                        {
                            overwriteAll = true;   // 이후 충돌은 자동 덮어쓰기
                        }
                        // 예 → 덮어쓰기(복사/이동 분리 — 지금은 동일, 향후 분기 가능). I/O는 백그라운드.
                        string dest = FileOps.NaturalDest(destDir, p);
                        await Task.Run(() => { if (copy) { OverwriteCopy(p, dest, OnBytes, ct); } else { OverwriteMove(p, dest, OnBytes, ct); } });
                        performed.Add((p, dest));   // 주의: 덮어쓴 기존 파일의 복원은 불가(undo=역방향만, 탐색기 동급 한계)
                        done++;
                    }
                    else
                    {
                        // 충돌 없음 → 순번 없이 대상 이름으로 처리. I/O는 백그라운드(UI 무블록).
                        string dest = FileOps.NaturalDest(destDir, p);
                        await Task.Run(() =>
                        {
                            if (copy) { FileOps.CopyOntoWithProgress(p, dest, overwrite: false, OnBytes, ct); }
                            else { FileOps.MoveOntoWithProgress(p, dest, overwrite: false, OnBytes, ct); }
                        });
                        performed.Add((p, dest));
                        done++;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;   // 취소 버튼/창 닫기 → 남은 항목 중단
                    break;
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
            }
        }
        finally
        {
            string summary = cancelled
                ? $"{verb} 취소됨 — {done}개 완료"
                : err is null
                    ? $"{verb} 완료 — {done}개{(skipped > 0 ? $" · 건너뜀 {skipped}개" : string.Empty)}"
                    : $"{verb} 일부 실패: {err}";
            // 성공(무취소·무오류)만 2초 카운트다운 자동 닫기 — 실패/취소는 결과 확인 위해 유지.
            win.Complete(summary, AppSettings.View.AutoCloseTransferWindow && !cancelled && err is null);
            StatusText.Text = summary;
        }
        // undo 기록(B-13u): 취소/부분 실패여도 "실제 수행된 항목"은 되돌릴 수 있어야 함(탐색기 동일).
        if (performed.Count > 0)
        {
            _history.Push(copy
                ? new CopyBatchOp(performed, $"복사 {performed.Count}개", FileOps.DeleteToRecycleBin)
                : new MoveBatchOp(performed, $"이동 {performed.Count}개"));
        }
        _ = sourceLeft;   // 향후 정밀 갱신용(현재는 양쪽 재로드)
        ReloadBothPanels();   // 양쪽 갱신(BUG-006 임시 — watcher 전까지)
    }

    /// <summary>덮어쓰기 <b>복사</b>(진행 보고 · 현재 이동과 동일 처리 — 향후 분기 가능하도록 분리, 사용자 요청).</summary>
    private static void OverwriteCopy(string src, string dest, Action<long>? onBytes, CancellationToken ct)
        => FileOps.CopyOntoWithProgress(src, dest, overwrite: true, onBytes, ct);

    /// <summary>덮어쓰기 <b>이동</b>(진행 보고 · 현재 복사와 동일 처리 — 향후 분기 가능하도록 분리, 사용자 요청).</summary>
    private static void OverwriteMove(string src, string dest, Action<long>? onBytes, CancellationToken ct)
        => FileOps.MoveOntoWithProgress(src, dest, overwrite: true, onBytes, ct);

    // 덮어쓰기 확인은 진행 창 안에서 처리(TransferProgressWindow.AskOverwriteAsync) — ContentDialog XamlRoot 문제 회피.

    private bool _activeLeft = true;
    private bool _windowActive = true;

    /// <summary>활성(포커스) 패널 전환.</summary>
    private void SetActivePanel(bool left)
    {
        _activeLeft = left;
        RefreshSelectionFocus();
        _session?.MarkDirty();   // 활성 패널 변경 → 세션 저장 예약
    }

    /// <summary>탭 바(빈 영역) 클릭 → 그 패널을 활성화하고 목록에 포커스(키보드 이동 대상).</summary>
    private void OnTabBarTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag)
        {
            bool left = tag == "L";
            SetActivePanel(left);
            Panel(left).Grid.Focus(FocusState.Programmatic);
        }
    }

    // ── 패널 탭 ─────────────────────────────────────────────────────
    // 각 탭 = PanelTab(경로·이동기록·펼침상태). 활성 탭이 그 패널의 현재 뷰.

    /// <summary>지정 패널의 활성 탭을 <paramref name="tab"/>로 전환하고 그 탭의 경로/상태로 뷰를 갱신한다.</summary>
    private void SwitchToTab(bool left, PanelTab tab)
    {
        var active = Panel(left).Active;
        if (!ReferenceEquals(active, tab))
        {
            active.IsActive = false;
            tab.IsActive = true;
            Panel(left).Active = tab;
        }
        SetActivePanel(left);
        ShowTab(left, tab);   // 이미 로드된 탭이면 재-Open 없이 ItemsSource 스왑(성능 슬라이스 4-2)
        _session?.MarkDirty();   // 활성 탭 전환 → 세션 저장 예약
    }

    /// <summary>
    /// 탭을 화면에 표시한다. 탭이 이미 열려(캐시) 있으면 <b>재-Open 없이</b> 그리드의 ItemsSource만
    /// 그 탭의 컬렉션으로 스왑하고 경로바·헤더·네비 버튼을 복원한다(탭 전환 지연 제거, QA #4).
    /// 아직 로드 전이면 <see cref="LoadDirectory"/>로 연다.
    /// </summary>
    private void ShowTab(bool left, PanelTab tab)
    {
        if (tab.Loaded && tab.Items.Handle != IntPtr.Zero)
        {
            var (grid, header, pathBar) = PanelUi(left);
            if (Panel(left).SortKeys is { } sk)
            {
                tab.Items.SetSort(sk, foldersFirst: true);   // 이 패널 정렬을 캐시 탭에도 반영(전환 일관, 바인딩 전이라 무플래시)
            }
            grid.ItemsSource = tab.Items;   // 열린 핸들 재사용 — 재열거·재펼침 없음
            grid.ScrollToTop();             // ItemsSource 교체 후 뷰포트 확정(잔존 오프셋으로 빈 화면 방지)
            pathBar.Path = tab.Current;
            header.Text = $"{tab.Current} — {tab.DirectChildCount}개 항목";
            UpdateSelectionCount(tab.Items);
            ArmWatcher(left);   // 캐시된 탭으로 전환 → 그 폴더 감시로 갱신(B-12w)
        }
        else
        {
            LoadDirectory(left, tab);
        }
        UpdateNavButtons(left);
    }

    /// <summary>새 탭 추가(현재 탭 경로 기준, 없으면 홈) 후 그 탭으로 전환.</summary>
    private void AddTab(bool left)
    {
        var basePath = Panel(left).Active.Current;
        if (string.IsNullOrEmpty(basePath))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        var tab = new PanelTab();
        tab.Nav.NavigateTo(basePath, record: false);   // 새 탭 초기 경로(기록 없음)
        tab.Title = PathDisplay.TabTitle(basePath);     // 탭 이름 즉시 설정(전환 전 공백 방지, 버그 수정)
        Panel(left).Tabs.Add(tab);
        SwitchToTab(left, tab);
    }

    /// <summary>탭 클릭 → 그 탭으로 전환.</summary>
    private void OnTabTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PanelTab tab)
        {
            bool left = _left.Tabs.Contains(tab);
            SwitchToTab(left, tab);
            Panel(left).Grid.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    /// <summary>탭 영역 더블클릭 → 새 탭 추가(+ 버튼 대체).</summary>
    private void OnTabBarDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag)
        {
            AddTab(tag == "L");
            e.Handled = true;
        }
    }

    /// <summary>탭 닫기 — 최소 1개는 유지. 활성 탭을 닫으면 이웃 탭으로 전환.</summary>
    private void CloseTab(bool left, PanelTab tab)
    {
        var tabs = Panel(left).Tabs;
        int idx = tabs.IndexOf(tab);
        if (idx < 0 || tabs.Count <= 1)
        {
            return;   // 없거나 마지막 하나면 닫지 않음
        }
        bool wasActive = ReferenceEquals(tab, Panel(left).Active);
        tabs.RemoveAt(idx);
        tab.Items.Dispose();   // 닫힌 탭의 코어 트리 핸들 해제(캐시 메모리 회수, NFR-R)
        if (wasActive)
        {
            SwitchToTab(left, tabs[Math.Min(idx, tabs.Count - 1)]);
        }
        _session?.MarkDirty();   // 탭 닫기 → 세션 저장 예약
    }

    /// <summary>탭 더블클릭 → 설정된 동작(기본: 닫기). 빈 영역 더블클릭(추가)과 구분되도록 여기서 소비.</summary>
    private void OnTabDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not PanelTab tab)
        {
            return;
        }
        e.Handled = true;   // 탭 위 더블클릭은 탭 바(추가)로 전파하지 않음
        bool left = _left.Tabs.Contains(tab);
        switch (AppSettings.Tab.DoubleClick)
        {
            case TabDoubleClickAction.Close:
                CloseTab(left, tab);
                break;
            case TabDoubleClickAction.None:
                break;
            default:   // Favorite/PopupMenu — 후속
                StatusText.Text = "탭 동작(즐겨찾기/팝업 메뉴)은 후속 구현";
                break;
        }
    }

    /// <summary>선택 항목 포커스색 갱신 — (윈도우 활성 &amp;&amp; 그 패널 활성)일 때만 파랑, 아니면 회색.</summary>
    private void RefreshSelectionFocus()
    {
        _left.Items.SetPanelFocused(_windowActive && _activeLeft);
        _right.Items.SetPanelFocused(_windowActive && !_activeLeft);
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
    {
        _windowActive = e.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
        RefreshSelectionFocus();
    }

    /// <summary>선택 개수를 세어 상태바에 표시(코어 선택 수).</summary>
    private void UpdateSelectionCount(VirtualTreeCollection items)
    {
        int count = items.SelectionCount;
        StatusText.Text = count > 0 ? $"{count}개 선택됨" : "준비됨";
        RefreshBottomDocks();   // 선택 변경 → 하단 정보 뷰 갱신 (BP-2)
    }

    /// <summary>
    /// 키보드 이동: ↑/↓ 선택 이동(Shift=범위 확장, Ctrl=위치만 이동), →/← 폴더 펼침/접힘,
    /// Space=캐럿 항목 선택(Ctrl+Space=비연속 다중 선택 토글). 대상은 활성 패널(포커스 비의존).
    /// </summary>
    /// <summary>키보드를 입력 컨트롤(터미널·텍스트박스)이 소유 중인가 — 그렇다면 전역 파일목록 단축키는 개입 금지.</summary>
    private bool IsKeyboardOwnedByInput()
    {
        var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot) as DependencyObject;
        while (focused is not null)
        {
            if (focused is Terminal.TerminalView or TextBox)
            {
                return true;
            }
            focused = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(focused);
        }
        return false;
    }

    private void OnGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 터미널·텍스트 입력이 포커스면 전역 파일목록 단축키는 개입 안 함(터미널이 키를 소유).
        // (RootGrid 핸들러는 handledEventsToo=true라 터미널이 Handled해도 여기까지 오므로 명시적으로 차단.)
        if (IsKeyboardOwnedByInput())
        {
            return;
        }

        // Ctrl+W: 활성 패널의 활성 탭 닫기.
        if (e.Key == VirtualKey.W && IsCtrlDown())
        {
            CloseTab(_activeLeft, Panel(_activeLeft).Active);
            e.Handled = true;
            return;
        }

        // Ctrl+` (VK_OEM_3): 하단 패널 표시/숨김 토글(설계 FR-K2b, BP-1).
        if (e.Key == (VirtualKey)0xC0 && IsCtrlDown())
        {
            ToggleBottomPanel();
            e.Handled = true;
            return;
        }

        // F2: 캐럿 항목 인라인 이름 변경(표준 단축키). 캐럿 행이 실체화돼 있을 때.
        if (e.Key == VirtualKey.F2)
        {
            e.Handled = true;
            int ci = Panel(_activeLeft).Items.CaretIndex;
            if (Panel(_activeLeft).Items.CaretItem is DirItem it && ci >= 0
                && Panel(_activeLeft).Grid.RowElement(ci) is FrameworkElement row)
            {
                BeginRename(row, it, _activeLeft);
            }
            return;
        }

        // F5: 활성 패널 수동 새로고침(watcher 자동 갱신 전까지 수동 갱신 수단, B-12w 부분).
        if (e.Key == VirtualKey.F5)
        {
            e.Handled = true;
            ReloadPanel(_activeLeft);
            return;
        }

        // Ctrl+C/X/V: 복사/잘라내기/붙여넣기(활성 패널, 대상=선택 또는 캐럿).
        if (IsCtrlDown() && (e.Key == VirtualKey.C || e.Key == VirtualKey.X || e.Key == VirtualKey.V))
        {
            e.Handled = true;
            switch (e.Key)
            {
                case VirtualKey.C: CopyPaths(_activeLeft, KeyboardTargets(_activeLeft)); break;
                case VirtualKey.X: CutPaths(_activeLeft, KeyboardTargets(_activeLeft)); break;
                case VirtualKey.V: PasteInto(_activeLeft); break;
            }
            return;
        }

        // Ctrl+Z/Y: 파일 작업 실행 취소/다시 실행(B-13u, 탐색기 표준). Ctrl+Shift+Z=다시 실행(관용 별칭).
        // (이름변경 편집기 등 TextBox 포커스면 상단 IsKeyboardOwnedByInput 가드가 이미 차단 — TextBox 자체 undo 유지.)
        if (IsCtrlDown() && e.Key == VirtualKey.Z)
        {
            e.Handled = true;
            if (IsShiftDown()) { DoRedo(); } else { DoUndo(); }
            return;
        }
        if (IsCtrlDown() && e.Key == VirtualKey.Y)
        {
            e.Handled = true;
            DoRedo();
            return;
        }

        // Delete=휴지통(되돌리기 가능), Shift+Delete=완전 삭제(확인). 선택(또는 캐럿) 대상.
        if (e.Key == VirtualKey.Delete)
        {
            e.Handled = true;
            DeletePaths(_activeLeft, KeyboardTargets(_activeLeft), permanent: IsShiftDown());
            return;
        }

        bool space = e.Key == VirtualKey.Space;
        bool vertical = e.Key == VirtualKey.Up || e.Key == VirtualKey.Down;
        bool horizontal = e.Key == VirtualKey.Left || e.Key == VirtualKey.Right;
        if (!space && !vertical && !horizontal)
        {
            return;
        }
        bool left = _activeLeft;   // 활성 패널 기준(포커스 비의존) — 탭/행 클릭이 활성 패널을 정함
        var items = Panel(left).Items;

        // Alt+방향키 네비게이션: ↑=위로, ←/→=뒤로/앞으로, ↓=활성화. **목록이 비어도 동작**(패널/탭 이동은 목록 무관).
        if (IsAltDown())
        {
            switch (e.Key)
            {
                case VirtualKey.Up: GoUp(left); e.Handled = true; return;
                case VirtualKey.Left: GoBack(left); e.Handled = true; return;
                case VirtualKey.Right: GoForward(left); e.Handled = true; return;
                case VirtualKey.Down:
                {
                    var c = items.CaretItem;
                    if (c is not null) { ActivateItem(left, c); }
                    e.Handled = true;
                    return;
                }
            }
        }

        if (items.Count == 0)
        {
            return;   // 여기서부터는 목록 항목이 필요한 동작(선택 이동/펼침/Space)
        }
        SetActivePanel(left);
        int cur = items.CaretIndex;
        bool ctrl = IsCtrlDown();
        bool shift = IsShiftDown();
        var grid = Panel(left).Grid;

        if (space)
        {
            // 캐럿 항목 선택. Ctrl+Space=토글(비연속 다중, 나머지 유지), Space=단일 선택.
            if (cur >= 0)
            {
                items.Select(items[cur], (uint)(ctrl ? 1 : 0));
                UpdateSelectionCount(items);
            }
            e.Handled = true;
            return;
        }

        if (horizontal)
        {
            if (e.Key == VirtualKey.Right)
            {
                // →: 현재 폴더 펼침(폴더가 아니거나 이미 펼쳤으면 무시).
                if (cur >= 0 && items[cur].IsDir && !items[cur].IsExpanded)
                {
                    ToggleExpandRow(left, items[cur]);
                }
            }
            else if (cur >= 0)
            {
                // ←: 펼쳐진 폴더면 접기. 접힌 폴더/파일이면 **상위(부모) 폴더로 이동**(단일 선택).
                var it = items[cur];
                if (it.IsDir && it.IsExpanded)
                {
                    ToggleExpandRow(left, it);
                }
                else
                {
                    int p = ParentIndex(items, cur);
                    if (p >= 0)
                    {
                        items.Select(items[p], 0);
                        items.SetCaret(p);
                        grid.BringIndexIntoView(p);
                        UpdateSelectionCount(items);
                    }
                }
            }
            e.Handled = true;
            return;
        }

        // 세로 이동(캐럿 기준 한 칸). 캐럿 없으면 맨 위부터.
        int next = cur < 0 ? 0 : (e.Key == VirtualKey.Down ? cur + 1 : cur - 1);
        if (next < 0)
        {
            next = 0;
        }
        if (next >= items.Count)
        {
            next = items.Count - 1;
        }

        if (ctrl && !shift)
        {
            // 비연속 다중 선택 모드: 선택은 그대로 두고 캐럿(위치)만 이동. Space로 개별 토글.
            items.SetCaret(next);
            grid.BringIndexIntoView(next);
            e.Handled = true;
            return;
        }

        if (shift)
        {
            items.SelectRange(items[next]);   // 코어 anchor~next 범위(anchor 유지)
        }
        else
        {
            items.Select(items[next], 0);     // 단일(anchor=next)
        }
        items.SetCaret(next);
        grid.BringIndexIntoView(next);
        UpdateSelectionCount(items);
        e.Handled = true;
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DirItem item)
        {
            item.IsHovered = true;
        }
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DirItem item)
        {
            item.IsHovered = false;
        }
    }

    private static bool IsCtrlDown() => KeyDown(VirtualKey.Control);

    private static bool IsShiftDown() => KeyDown(VirtualKey.Shift);

    private static bool KeyDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    // ── 레이아웃 토글 (영역 숨김/표시) ──────────────────────────────
    // 숨길 때 해당 splitter와 행/열 크기를 함께 0으로 만들어 빈 공간을 남기지 않는다(docs/20 §2).

    private void OnToggleLauncher(object sender, RoutedEventArgs e)
        => LauncherBar.Visibility = Vis(ToggleLauncherBtn.IsChecked);

    private void OnToggleRightPanel(object sender, RoutedEventArgs e)
    {
        bool show = ToggleRightBtn.IsChecked == true;
        RightPanel.Visibility = Vis(show);
        PanelSplitter.Visibility = Vis(show);
        SplitterCol.Width = show ? GridLength.Auto : new GridLength(0);
        // 표시 시 좌/우 동일 크기(star) 복원 + 최소폭. 숨김 시 MinWidth도 풀어 완전 접힘.
        RightCol.MinWidth = show ? 160 : 0;
        RightCol.Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        // 듀얼→단일(좌 마스터) 전환 시 하단 우 도킹도 연동해서 숨김
        UpdateBottomDock();
    }

    /// <summary>하단 패널 표시/숨김 토글(Ctrl+`, BP-1) — 토글 버튼 상태를 뒤집고 동일 경로로 반영.</summary>
    private void ToggleBottomPanel()
    {
        ToggleTerminalBtn.IsChecked = !(ToggleTerminalBtn.IsChecked == true);
        OnToggleTerminal(ToggleTerminalBtn, null!);
    }

    private void OnToggleTerminal(object sender, RoutedEventArgs e)
    {
        bool show = ToggleTerminalBtn.IsChecked == true;
        TerminalPanel.Visibility = Vis(show);
        TermSplitter.Visibility = Vis(show);
        TermSplitterRow.Height = show ? GridLength.Auto : new GridLength(0);
        // 숨김 시 MinHeight도 0으로 풀어 최소높이 빈 띠가 남지 않게 함(표시 시 80 복원).
        TermRow.MinHeight = show ? 80 : 0;
        TermRow.Height = show ? new GridLength(180) : new GridLength(0);
        if (show)
        {
            UpdateBottomDock();
        }
        _session?.MarkDirty();   // 하단 패널 표시/숨김 → 세션 저장 예약 (BP-1c)
    }

    /// <summary>하단 도킹 좌/우 분리 토글 → 실제 반영은 UpdateBottomDock가 정책적으로 결정.</summary>
    private void OnToggleBottomSplit(object sender, RoutedEventArgs e)
    {
        UpdateBottomDock();
        _session?.MarkDirty();   // 하단 좌/우 분리 → 세션 저장 예약 (BP-1c)
    }

    /// <summary>
    /// 하단 도킹의 좌/우 분리 상태를 패널 구성과 연동한다(docs/20).
    /// - 하단 우 도킹은 **우 패널이 표시(듀얼)이고** 하단 분리가 켜졌을 때만 보인다.
    /// - 우 패널을 숨기면(단일=좌 마스터) 하단 우 도킹도 숨기고 "분리" 토글은 비활성화.
    /// </summary>
    private void UpdateBottomDock()
    {
        bool dual = ToggleRightBtn.IsChecked == true;          // 우 패널 표시 = 듀얼
        bool split = ToggleBottomSplitBtn.IsChecked == true;   // 하단 좌/우 분리 요청
        // 분리는 듀얼일 때만 의미 있음
        ToggleBottomSplitBtn.IsEnabled = dual;
        bool showRightDock = dual && split;

        BottomRightDock.Visibility = Vis(showRightDock);
        BottomSplitter.Visibility = Vis(showRightDock);
        BottomSplitterCol.Width = showRightDock ? GridLength.Auto : new GridLength(0);
        // 표시 시 최소폭 유지, 숨김 시 MinWidth도 풀어 완전 접힘.
        BottomRightCol.MinWidth = showRightDock ? 160 : 0;
        BottomRightCol.Width = showRightDock ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private static Visibility Vis(bool? on)
        => on == true ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>하단 도킹 패널의 정보 콘텐츠를 각 패널의 현재 폴더로 갱신한다(BP-1). 콘텐츠 종류=정보일 때 표시.</summary>
    private void RefreshBottomDocks()
    {
        if (BottomLeftDockView is null || BottomRightDockView is null)
        {
            return;   // ctor 초기화 순서 방어
        }
        BottomLeftDockView.InfoText = DockInfo(_left);
        BottomRightDockView.InfoText = DockInfo(_right);
        BottomLeftDockView.PreviewPath = PreviewTarget(_left);
        BottomRightDockView.PreviewPath = PreviewTarget(_right);
        BottomLeftDockView.CurrentFolder = _left.Active.Current;    // 터미널 작업 디렉터리 등 (BP-T)
        BottomRightDockView.CurrentFolder = _right.Active.Current;
    }

    /// <summary>미리보기 대상 — 단일 선택된 <b>파일</b>의 경로(폴더/다중/없음은 빈 문자열). (BP-2)</summary>
    private static string PreviewTarget(PanelView p)
    {
        var items = p.Active.Items;
        if (items.SelectionCount == 1 && items.CaretItem is DirItem it && !it.IsDir)
        {
            return it.FullPath;
        }
        return string.Empty;
    }

    /// <summary>도킹 정보 텍스트(BP-2) — 선택/캐럿 항목의 속성(이름·종류·크기·수정·경로),
    /// 다중 선택이면 개수, 선택 없으면 현재 폴더. (후속: 총 크기·미리보기·터미널.)</summary>
    private static string DockInfo(PanelView p)
    {
        var items = p.Active.Items;
        int selCount = items.SelectionCount;
        if (selCount >= 2)
        {
            return $"선택: {selCount}개 항목";
        }
        if (selCount == 1 && items.CaretItem is DirItem it)
        {
            var lines = new List<string?>
            {
                it.Name,
                $"종류: {it.KindText}",
                it.IsDir ? null : $"크기: {it.SizeLabel}",
                string.IsNullOrEmpty(it.ModifiedDateTimeLabel) ? null : $"수정: {it.ModifiedDateTimeLabel}",
                $"경로: {it.FullPath}",
            };
            return string.Join("\n", lines.Where(s => !string.IsNullOrEmpty(s)));
        }
        string cur = p.Active.Current;
        return string.IsNullOrEmpty(cur) ? "(폴더 없음)" : $"현재 폴더:\n{cur}";
    }

    // ── 스플리터 스냅 (자석식) ─────────────────────────────────────
    // 좌/우 분리선을 ① 창 중앙(50:50) ② 상↔하 분리선 위치에 자석처럼 정렬한다(양방향).
    // 분리선 위치 = 좌 열의 ActualWidth. 임계 내면 스냅, 벗어나면 해제(24px).
    private const double SnapPx = 20;
    private bool _snapping;   // 스냅으로 Width를 되쓸 때 SizeChanged 재진입 방지

    /// <summary>상단 좌/우 분리선 이동 → 중앙 또는 하단 분리선 위치로 스냅.</summary>
    private void OnTopSplitSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_snapping || IsAltDown() || ToggleRightBtn.IsChecked != true) return;   // Alt=스냅 임시 해제, 듀얼일 때만
        double left = LeftCol.ActualWidth, total = left + RightCol.ActualWidth;
        if (total <= 0) return;
        double? target = SnapTarget(left, total,
            BottomRightDock.Visibility == Visibility.Visible ? BottomLeftCol.ActualWidth : (double?)null);
        ApplySnap(LeftCol, RightCol, target, left, total);
    }

    /// <summary>하단 좌/우 분리선 이동 → 중앙 또는 상단 분리선 위치로 스냅.</summary>
    private void OnBottomSplitSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_snapping || IsAltDown() || BottomRightDock.Visibility != Visibility.Visible) return;   // Alt=스냅 임시 해제, 분리(듀얼)일 때만
        double left = BottomLeftCol.ActualWidth, total = left + BottomRightCol.ActualWidth;
        if (total <= 0) return;
        double? target = SnapTarget(left, total,
            ToggleRightBtn.IsChecked == true ? LeftCol.ActualWidth : (double?)null);
        ApplySnap(BottomLeftCol, BottomRightCol, target, left, total);
    }

    /// <summary>Alt 키가 눌린 상태인가 — 눌린 동안 스냅을 임시 해제(정밀 드래그).</summary>
    private static bool IsAltDown()
        => (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down)
           == CoreVirtualKeyStates.Down;

    /// <summary>중앙(우선) 또는 상대 분리선 위치가 임계 내면 그 위치를 반환, 아니면 null.</summary>
    private static double? SnapTarget(double boundary, double total, double? align)
    {
        double mid = total / 2;
        if (Math.Abs(boundary - mid) <= SnapPx) return mid;
        if (align is double a && a > 0 && Math.Abs(boundary - a) <= SnapPx) return a;
        return null;
    }

    /// <summary>좌/우 열 폭을 target:(total-target) 비율(star)로 되써 분리선을 스냅한다.</summary>
    private void ApplySnap(ColumnDefinition a, ColumnDefinition b, double? target, double current, double total)
    {
        if (target is not double t || Math.Abs(t - current) < 0.5) return;
        _snapping = true;
        a.Width = new GridLength(t, GridUnitType.Star);
        b.Width = new GridLength(total - t, GridUnitType.Star);
        _snapping = false;
    }
}
