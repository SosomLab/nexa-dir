using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
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

    public MainWindow()
    {
        InitializeComponent();
        // 좌/우 패널이 같은 컬럼 인스턴스를 공유 → 리사이즈가 헤더·본문·양쪽 패널에 동시 반영(A3/A4).
        // 표시 순서 = 이름 · 수정한 날짜 · 종류 · 크기(Finder 스타일).
        foreach (var key in new[] { "ColName", "ColDate", "ColKind", "ColSize" })
        {
            var col = (NexaGridColumn)RootGrid.Resources[key];
            DirGrid.Columns.Add(col);
            DirGrid2.Columns.Add(col);
        }
        // 방향키 이동: UserControl 포커스 경로에 의존하지 않도록 최상위 RootGrid에서 받는다(활성 패널 기준).
        // handledEventsToo=true → 내부 ScrollViewer가 방향키를 먼저 처리(Handled)해도 항상 수신.
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnGridKeyDown), handledEventsToo: true);
        // 마우스 뒤로/앞으로(XButton1/2) → 활성 패널 탭 네비게이션(FR-I2 기본 바인딩, docs/26 §5-4).
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnRootPointerPressed), handledEventsToo: true);
        ShowInteropRoundTrip();
        // 좌/우 PanelView 구성(XAML 요소 참조 묶기). 이후 모든 패널 접근은 Panel(left) 경유.
        _left = new PanelView { IsLeft = true, Grid = DirGrid, Header = DirHeader, PathBar = PathBarL, TabStrip = LeftTabs };
        _right = new PanelView { IsLeft = false, Grid = DirGrid2, Header = DirHeader2, PathBar = PathBarR, TabStrip = RightTabs };
        // 그리드 행 수명 → 아이콘 지연 로드/취소. 행이 화면 밖으로 나가면 큐에서 제거(빠른 스크롤 부하 제한, P6).
        _iconCache = new ShellIconCache(DispatcherQueue);
        foreach (var p in new[] { _left, _right })
        {
            p.Grid.RowRealized += it => { if (it is DirItem d) { _iconCache.Request(d); } };
            p.Grid.RowRecycled += it => { if (it is DirItem d) { _iconCache.Cancel(d); } };
        }
        // 드래그 빈 영역 드롭 → 그 패널 현재 폴더로 이동(좌우 겸용, B-12).
        DirGrid.BodyDropped += () => OnPanelBackgroundDrop(true);
        DirGrid2.BodyDropped += () => OnPanelBackgroundDrop(false);
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
        // 좌/우 패널 모두 파일 목록 표시(초안: 좌=홈, 우=문서).
        Navigate(true, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), record: false);
        Navigate(false, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), record: false);
        UpdateBottomDock();
        Activated += OnWindowActivated;   // 윈도우 포커스 상실 시 선택 회색화
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
            result = await Task.Run(() =>
                VirtualTreeCollection.OpenAndExpand(path, v.ShowHiddenFiles, v.ShowDotFiles, expanded));
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
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }
        var props = e.GetCurrentPoint((UIElement)sender).Properties;
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
        var p = (path ?? string.Empty).Trim();
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

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // 마우스 뒤로/앞으로(XButton1/2)는 네비게이션 전용 → 행 선택·활성 패널 변경 금지(RootGrid가 처리).
        var pp = e.GetCurrentPoint((UIElement)sender).Properties;
        if (pp.IsXButton1Pressed || pp.IsXButton2Pressed)
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
        bool plain = !IsShiftDown() && !IsCtrlDown();

        // 선택 후 재클릭(더블클릭 아님) → 인라인 이름 변경. 직전 plain 클릭으로 단독 선택된 같은 항목을
        // 시스템 더블클릭 시간 이후 다시 클릭했을 때만 발동(빠른 재클릭=더블클릭=진입과 구분).
        if (plain && ReferenceEquals(_renameArm, item) && item.IsSelected && items.SelectionCount == 1
            && (DateTime.UtcNow - _renameArmAt).TotalMilliseconds > GetDoubleClickTime())
        {
            _renameArm = null;
            e.Handled = true;
            BeginRename(fe, item, left);
            return;
        }

        SetActivePanel(left);   // 클릭한 패널이 활성 → 반대 패널 선택은 회색(포커스아웃)
        Panel(left).Grid.Focus(FocusState.Programmatic);   // 키보드 이동 대상 포커스

        // 선택은 코어(OrderedSet)에 위임: Shift=가시 범위(anchor~클릭), Ctrl=토글, 그 외=단일.
        if (IsShiftDown())
        {
            items.SelectRange(item);
        }
        else if (IsCtrlDown())
        {
            items.Select(item, 1);   // 토글
        }
        else
        {
            items.Select(item, 0);   // 단일
        }
        int idx = items.IndexOf(item);
        if (idx >= 0)
        {
            items.SetCaret(idx);
        }
        UpdateSelectionCount(items);

        // plain 클릭으로 단독 선택된 항목을 이름변경 후보로 장전(다음 재클릭 시 편집).
        _renameArm = plain ? item : null;
        _renameArmAt = DateTime.UtcNow;
    }

    // 인라인 이름변경 트리거 상태(직전 plain 클릭으로 단독 선택된 항목·시각).
    private DirItem? _renameArm;
    private DateTime _renameArmAt;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>인라인 이름변경 시작 — 이름 셀을 편집기로 전환하고 포커스 + 이름부(확장자 제외) 선택.</summary>
    private void BeginRename(FrameworkElement row, DirItem item, bool left)
    {
        SetActivePanel(left);
        item.EditName = item.Name;
        item.IsRenaming = true;
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
        _renameArm = null;   // 더블클릭(진입/실행)은 이름변경 후보 해제
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        ActivateItem(left, item);   // 폴더=진입, 파일=연결 프로그램 실행
        e.Handled = true;
    }

    /// <summary>항목 활성화: 폴더/심볼릭링크는 진입(더블클릭 동작), 파일은 연결 프로그램으로 실행한다.</summary>
    private async void ActivateItem(bool left, DirItem item)
    {
        if (item.IsDir || item.Kind == NexaFileKind.Symlink)
        {
            Navigate(left, item.FullPath, record: true);
            return;
        }
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(item.FullPath);
            await Launcher.LaunchFileAsync(file);   // 확장자 연결 프로그램으로 실행
        }
        catch (Exception ex)
        {
            StatusText.Text = $"실행 실패: {ex.Message}";
        }
    }

    // ── 컨텍스트 메뉴 · 파일 작업 (복사/잘라내기/붙여넣기/완전삭제) — FileOps/FileClipboard 위임 ─────

    /// <summary>행 우클릭 → 컨텍스트 메뉴(열기·잘라내기·복사·붙여넣기·삭제·이름 바꾸기). 클릭 항목이
    /// 현재 선택에 없으면 단일 선택으로 맞춘 뒤 표시.</summary>
    private void OnRowContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        SetActivePanel(left);

        var flyout = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = item.IsDir ? "열기" : "실행" };
        open.Click += (_, _) => ActivateItem(left, item);
        flyout.Items.Add(open);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var cut = new MenuFlyoutItem { Text = "잘라내기" };
        cut.Click += (_, _) => CutPaths(left, ContextTargets(left, item));
        flyout.Items.Add(cut);
        var copy = new MenuFlyoutItem { Text = "복사" };
        copy.Click += (_, _) => CopyPaths(left, ContextTargets(left, item));
        flyout.Items.Add(copy);
        var paste = new MenuFlyoutItem { Text = "붙여넣기", IsEnabled = FileClipboard.HasContent };
        paste.Click += (_, _) => PasteInto(left);
        flyout.Items.Add(paste);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var del = new MenuFlyoutItem { Text = "삭제(완전)" };
        del.Click += (_, _) => DeletePaths(left, ContextTargets(left, item));
        flyout.Items.Add(del);
        var rename = new MenuFlyoutItem { Text = "이름 바꾸기" };
        rename.Click += (_, _) => BeginRename(fe, item, left);
        flyout.Items.Add(rename);

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

    /// <summary>클립보드 내용을 지정 패널의 현재 폴더에 붙여넣는다(cut=이동·copy=복사). 완료 후 재로드.</summary>
    private void PasteInto(bool left)
    {
        if (!FileClipboard.HasContent)
        {
            return;
        }
        string destDir = Panel(left).Active.Current;
        if (string.IsNullOrEmpty(destDir))
        {
            return;
        }
        bool cut = FileClipboard.IsCut;
        int ok = 0;
        string? err = null;
        foreach (var src in FileClipboard.Paths)
        {
            try
            {
                _ = cut ? FileOps.MoveInto(src, destDir) : FileOps.CopyInto(src, destDir);
                ok++;
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
        }
        if (cut)
        {
            FileClipboard.Clear();
        }
        StatusText.Text = err is null ? $"{(cut ? "이동" : "복사")} {ok}개 완료" : $"붙여넣기 일부 실패: {err}";
        ReloadPanel(left);
    }

    /// <summary>대상 경로를 <b>완전 삭제</b>한다(휴지통 아님). 확인 대화상자 후 실행, 재로드.</summary>
    private async void DeletePaths(bool left, IReadOnlyList<string> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }
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
        int ok = 0;
        string? err = null;
        foreach (var p in targets)
        {
            try
            {
                FileOps.DeletePermanent(p);
                ok++;
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
        }
        StatusText.Text = err is null ? $"삭제 {ok}개 완료" : $"삭제 일부 실패: {err}";
        ReloadPanel(left);
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

    /// <summary>행 드래그 시작 → 대상(선택 또는 드래그 항목)을 앱 내부 드래그 상태에 담는다.</summary>
    private void OnRowDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            args.Cancel = true;
            return;
        }
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        var items = Panel(left).Active.Items;
        var sel = items.SelectedPaths();
        _dragPaths = sel.Count > 0 && sel.Any(p => PathEq(p, item.FullPath))
            ? new List<string>(sel)
            : new List<string> { item.FullPath };
        _dragSourceLeft = left;
        args.Data.SetText(string.Join("\n", _dragPaths));   // 시각 피드백/외부 호환용
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    /// <summary>폴더 행 위 드래그 → 자기 자신이 아닌 폴더면 이동 수락(캡션 표시).</summary>
    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DirItem item && item.IsDir
            && _dragPaths.Count > 0 && !_dragPaths.Any(p => PathEq(p, item.FullPath)))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = $"{item.Name}(으)로 이동";
            e.DragUIOverride.IsCaptionVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    /// <summary>폴더 행에 드롭 → 그 폴더로 이동(소비). 파일/빈 영역 드롭은 소비하지 않아 본문으로 버블(현재 폴더로, B-12).</summary>
    private void OnRowDrop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DirItem item && item.IsDir && _dragPaths.Count > 0)
        {
            bool destLeft = PanelUnderPointer(fe) ?? _activeLeft;
            MovePathsInto(_dragSourceLeft, _dragPaths, item.FullPath, destLeft);
            _dragPaths.Clear();
            e.Handled = true;   // 폴더 드롭만 소비
        }
    }

    /// <summary>패널 빈 영역(행이 소비하지 않은 곳)에 드롭 → 그 패널의 현재 폴더로 이동(좌우 겸용, B-12).</summary>
    private void OnPanelBackgroundDrop(bool destLeft)
    {
        if (_dragPaths.Count == 0)
        {
            return;
        }
        string destDir = Panel(destLeft).Active.Current;
        if (!string.IsNullOrEmpty(destDir))
        {
            MovePathsInto(_dragSourceLeft, _dragPaths, destDir, destLeft);
        }
        _dragPaths.Clear();
    }

    /// <summary><paramref name="paths"/>를 <paramref name="destDir"/>로 이동하고 관련 패널을 재로드한다(제자리 제외).</summary>
    private void MovePathsInto(bool sourceLeft, IReadOnlyList<string> paths, string destDir, bool destLeft)
    {
        int ok = 0;
        string? err = null;
        foreach (var p in paths)
        {
            try
            {
                string? parent = System.IO.Path.GetDirectoryName(p.TrimEnd('\\', '/'));
                if (parent is not null && PathEq(parent, destDir))
                {
                    continue;   // 제자리 이동
                }
                FileOps.MoveInto(p, destDir);
                ok++;
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
        }
        StatusText.Text = err is null ? $"이동 {ok}개" : $"이동 일부 실패: {err}";
        ReloadPanel(sourceLeft);
        if (destLeft != sourceLeft)
        {
            ReloadPanel(destLeft);
        }
    }

    private bool _activeLeft = true;
    private bool _windowActive = true;

    /// <summary>활성(포커스) 패널 전환.</summary>
    private void SetActivePanel(bool left)
    {
        _activeLeft = left;
        RefreshSelectionFocus();
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
            grid.ItemsSource = tab.Items;   // 열린 핸들 재사용 — 재열거·재펼침 없음
            grid.ScrollToTop();             // ItemsSource 교체 후 뷰포트 확정(잔존 오프셋으로 빈 화면 방지)
            pathBar.Path = tab.Current;
            header.Text = $"{tab.Current} — {tab.DirectChildCount}개 항목";
            UpdateSelectionCount(tab.Items);
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
    }

    /// <summary>
    /// 키보드 이동: ↑/↓ 선택 이동(Shift=범위 확장, Ctrl=위치만 이동), →/← 폴더 펼침/접힘,
    /// Space=캐럿 항목 선택(Ctrl+Space=비연속 다중 선택 토글). 대상은 활성 패널(포커스 비의존).
    /// </summary>
    private void OnGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+W: 활성 패널의 활성 탭 닫기.
        if (e.Key == VirtualKey.W && IsCtrlDown())
        {
            CloseTab(_activeLeft, Panel(_activeLeft).Active);
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

        // Delete: 선택(또는 캐럿) 항목 완전 삭제(확인 대화상자).
        if (e.Key == VirtualKey.Delete)
        {
            e.Handled = true;
            DeletePaths(_activeLeft, KeyboardTargets(_activeLeft));
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
    }

    /// <summary>하단 도킹 좌/우 분리 토글 → 실제 반영은 UpdateBottomDock가 정책적으로 결정.</summary>
    private void OnToggleBottomSplit(object sender, RoutedEventArgs e) => UpdateBottomDock();

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
