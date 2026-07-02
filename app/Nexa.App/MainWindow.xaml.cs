using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Nexa.Controls;

namespace Nexa.App;

/// <summary>메인 윈도우 — 레이아웃 골격(docs/20) + 좌/우 패널 디렉터리 목록(F4/F5).</summary>
public sealed partial class MainWindow : Window
{
    // 패널별 가상화 목록: 코어 트리(nexa-tree)의 가시 노드 평면 스트림을 소비(C1, docs/29).
    // 선택(OrderedSet)·캐럿·펼침 상태는 코어/컬렉션이 소유 — MainWindow는 위임만 한다.
    private readonly VirtualTreeCollection _leftItems = new();
    private readonly VirtualTreeCollection _rightItems = new();

    // 패널별 탭: 각 탭(PanelTab)이 자체 이동 기록·펼침 상태를 가짐. 활성 탭이 현재 뷰.
    private readonly ObservableCollection<PanelTab> _leftTabs = new();
    private readonly ObservableCollection<PanelTab> _rightTabs = new();
    private PanelTab _leftTab = new();
    private PanelTab _rightTab = new();
    // 기존 네비게이션 코드 유지용 접근자 — 활성 탭의 이동 기록.
    private PanelTab _leftNav => _leftTab;
    private PanelTab _rightNav => _rightTab;

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
        // 가상화 목록: 새 행이 실체화될 때 실제 셸 아이콘을 비동기 로드(폴백: 글리프).
        _leftItems.RowBuilt = it => _ = LoadIconAsync(it);
        _rightItems.RowBuilt = it => _ = LoadIconAsync(it);
        // 패널별 초기 탭 1개 + 탭 바 바인딩(멀티라인·고정크기 ItemsRepeater).
        _leftTab.IsActive = true;
        _rightTab.IsActive = true;
        _leftTabs.Add(_leftTab);
        _rightTabs.Add(_rightTab);
        LeftTabs.ItemsSource = _leftTabs;
        RightTabs.ItemsSource = _rightTabs;
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
    /// 코어 디렉터리 스트리밍 열거(nexa_dir_*)를 호출해 폴더 내용을 지정 패널 목록에 표시한다.
    /// 좌/우 패널이 같은 로직을 공유(패널별 목록/header/path). 실패는 헤더 메시지로 격리.
    /// 목록은 <see cref="VirtualTreeCollection"/> — 코어 트리(nexa-tree)의 가시 노드 평면 스트림을
    /// 가상화로 소비한다(보이는 행만 실체화). 가시성 필터(숨김/점)는 코어에 적용(F24).
    /// </summary>
    private void LoadDirectory(string path, VirtualTreeCollection items, NexaFileGrid grid, TextBlock header, NexaPathBar pathBar)
    {
        try
        {
            var v = AppSettings.View;
            bool ok = items.Open(path, v.ShowHiddenFiles, v.ShowDotFiles);
            grid.ItemsSource = items;
            pathBar.Path = path;
            int direct = items.Count;   // 직접 자식 수(펼침 재적용 전)
            if (ok)
            {
                // F18: 진입/이동에도 이전 펼침 상태 유지 — 탭별 경로셋을 얕은→깊은 순 재적용.
                var tab = ReferenceEquals(items, _leftItems) ? _leftTab : _rightTab;
                items.ExpandPaths(tab.Expanded);
            }
            grid.ScrollToTop();   // 진입 시 첫 항목이 맨 위(이전 폴더 스크롤 오프셋 잔존 방지). GoUp은 이후 SelectByPath가 대상 중앙 정렬로 덮어씀.
            header.Text = ok
                ? $"{path} — {direct}개 항목"
                : $"디렉터리 열기 실패: {path}";
        }
        catch (Exception ex)
        {
            header.Text = $"디렉터리 열거 실패: {ex.Message}";
        }
    }

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

    /// <summary>현재 경로 기준으로 좌·우 패널을 다시 열거한다(펼침 상태 유지). 설정 토글 반영용.</summary>
    private void ReloadBothPanels()
    {
        if (!string.IsNullOrEmpty(_leftTab.Current))
        {
            LoadDirectory(_leftTab.Current, _leftItems, DirGrid, DirHeader, PathBarL);
        }
        if (!string.IsNullOrEmpty(_rightTab.Current))
        {
            LoadDirectory(_rightTab.Current, _rightItems, DirGrid2, DirHeader2, PathBarR);
        }
    }

    // ── 네비게이션(패널/탭별 이동 기록: 뒤로·앞으로·위로) ─────────────

    /// <summary>지정 패널을 <paramref name="path"/>로 이동한다. record=true면 현재 위치를 뒤로 스택에 기록(앞으로 초기화).</summary>
    private void Navigate(bool left, string path, bool record)
    {
        var nav = left ? _leftNav : _rightNav;
        if (record && !string.IsNullOrEmpty(nav.Current))
        {
            nav.Back.Push(nav.Current);
            nav.Fwd.Clear();
        }
        nav.Current = path;
        nav.Title = TabTitle(path);   // 활성 탭 이름 갱신(탭 바 표시)
        if (left)
        {
            LoadDirectory(path, _leftItems, DirGrid, DirHeader, PathBarL);
        }
        else
        {
            LoadDirectory(path, _rightItems, DirGrid2, DirHeader2, PathBarR);
        }
        UpdateNavButtons(left);
    }

    private void GoBack(bool left)
    {
        var nav = left ? _leftNav : _rightNav;
        if (nav.Back.Count == 0)
        {
            return;
        }
        nav.Fwd.Push(nav.Current);
        Navigate(left, nav.Back.Pop(), record: false);
    }

    private void GoForward(bool left)
    {
        var nav = left ? _leftNav : _rightNav;
        if (nav.Fwd.Count == 0)
        {
            return;
        }
        nav.Back.Push(nav.Current);
        Navigate(left, nav.Fwd.Pop(), record: false);
    }

    private void GoUp(bool left)
    {
        var nav = left ? _leftNav : _rightNav;
        var from = nav.Current;   // 떠나는 폴더(=나) — 상위에서 이 폴더를 선택 상태로
        var parent = Directory.GetParent(from);
        if (parent is not null)
        {
            Navigate(left, parent.FullName, record: true);
            SetActivePanel(left);       // 그 패널을 활성으로(선택 포커스 색)
            SelectByPath(left, from);   // 상위 목록에서 방금 떠난 폴더를 선택·포커스
        }
    }

    /// <summary>지정 패널 목록에서 경로가 일치하는 항목을 단일 선택하고 캐럿·스크롤을 맞춘다(없으면 무시).</summary>
    private void SelectByPath(bool left, string fullPath)
    {
        var items = left ? _leftItems : _rightItems;
        int i = items.IndexOfPath(fullPath);
        if (i < 0)
        {
            return;
        }
        items.Select(items[i], 0);   // 단일 선택(코어 위임)
        items.SetCaret(i);
        // 방금 떠난 폴더(네비 대상)를 화면에 보이게 — 기본 가운데(설정 UpNavTargetAlign). 오프스크린도 강제 실체화.
        (left ? DirGrid : DirGrid2).ScrollIndexIntoView(i, AppSettings.View.UpNavTargetAlign);
        UpdateSelectionCount(items);
        RefreshSelectionFocus();
    }

    /// <summary>뒤로/앞으로/위로 버튼 활성 상태 갱신.</summary>
    private void UpdateNavButtons(bool left)
    {
        var nav = left ? _leftNav : _rightNav;
        bool up = !string.IsNullOrEmpty(nav.Current) && Directory.GetParent(nav.Current) is not null;
        if (left)
        {
            BackBtnL.IsEnabled = nav.Back.Count > 0;
            FwdBtnL.IsEnabled = nav.Fwd.Count > 0;
            UpBtnL.IsEnabled = up;
        }
        else
        {
            BackBtnR.IsEnabled = nav.Back.Count > 0;
            FwdBtnR.IsEnabled = nav.Fwd.Count > 0;
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
                Navigate(left, dir, record: true);   // 파일이면 그 파일의 상위 폴더로 이동
                SelectByPath(left, p);               // 그 파일 선택
                return;
            }
        }
        StatusText.Text = $"경로 없음: {p}";
        (left ? PathBarL : PathBarR).Path = (left ? _leftTab : _rightTab).Current;   // 현재 경로로 복귀
    }

    /// <summary>
    /// 항목의 실제 셸 아이콘(폴더 커스텀 아이콘·파일 형식 아이콘)을 비동기로 로드한다.
    /// 성공 시 <see cref="DirItem.IconImage"/> 설정(글리프→실제 아이콘 교체), 실패는 글리프 유지.
    /// </summary>
    private static async Task LoadIconAsync(DirItem item)
    {
        try
        {
            StorageItemThumbnail thumb = item.IsDir
                ? await (await StorageFolder.GetFolderFromPathAsync(item.FullPath)).GetThumbnailAsync(ThumbnailMode.ListView, 16)
                : await (await StorageFile.GetFileFromPathAsync(item.FullPath)).GetThumbnailAsync(ThumbnailMode.ListView, 16);
            if (thumb is not null && thumb.Type == ThumbnailType.Image)
            {
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(thumb);
                item.IconImage = bmp;
            }
        }
        catch
        {
            // 접근 불가·미지원 → 글리프 폴백 유지.
        }
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
    /// 갱신해 진입/이동에도 상태가 유지되게 한다(F18).
    /// </summary>
    private void ToggleExpandRow(bool left, DirItem item)
    {
        if (!item.IsDir)
        {
            return;
        }
        var items = left ? _leftItems : _rightItems;
        var set = (left ? _leftTab : _rightTab).Expanded;
        bool willExpand = !item.IsExpanded;
        items.ToggleExpand(item);   // 코어 위임 → diff 반영(Reset)
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
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        var items = left ? _leftItems : _rightItems;
        SetActivePanel(left);   // 클릭한 패널이 활성 → 반대 패널 선택은 회색(포커스아웃)
        (left ? DirGrid : DirGrid2).Focus(FocusState.Programmatic);   // 키보드 이동 대상 포커스

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
    }

    /// <summary>행 더블클릭 → 폴더면 해당 패널을 그 폴더로 이동(진입).</summary>
    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        if (!item.IsDir && item.Kind != NexaFileKind.Symlink)
        {
            return;   // 파일: 진입 대상 아님(향후 셸 실행)
        }
        bool left = PanelUnderPointer(fe) ?? _activeLeft;
        Navigate(left, item.FullPath, record: true);
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
            (left ? DirGrid : DirGrid2).Focus(FocusState.Programmatic);
        }
    }

    // ── 패널 탭 ─────────────────────────────────────────────────────
    // 각 탭 = PanelTab(경로·이동기록·펼침상태). 활성 탭이 그 패널의 현재 뷰.

    /// <summary>탭 표시 이름 = 폴더명(루트/드라이브면 경로 자체).</summary>
    private static string TabTitle(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    /// <summary>지정 패널의 활성 탭을 <paramref name="tab"/>로 전환하고 그 탭의 경로/상태로 뷰를 갱신한다.</summary>
    private void SwitchToTab(bool left, PanelTab tab)
    {
        var active = left ? _leftTab : _rightTab;
        if (!ReferenceEquals(active, tab))
        {
            active.IsActive = false;
            tab.IsActive = true;
            if (left) { _leftTab = tab; }
            else { _rightTab = tab; }
        }
        SetActivePanel(left);
        Navigate(left, tab.Current, record: false);   // 활성 탭 경로 로드(펼침 상태 유지)
    }

    /// <summary>새 탭 추가(현재 탭 경로 기준, 없으면 홈) 후 그 탭으로 전환.</summary>
    private void AddTab(bool left)
    {
        var basePath = (left ? _leftTab : _rightTab).Current;
        if (string.IsNullOrEmpty(basePath))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        var tab = new PanelTab { Current = basePath };
        (left ? _leftTabs : _rightTabs).Add(tab);
        SwitchToTab(left, tab);
    }

    /// <summary>탭 클릭 → 그 탭으로 전환.</summary>
    private void OnTabTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PanelTab tab)
        {
            bool left = _leftTabs.Contains(tab);
            SwitchToTab(left, tab);
            (left ? DirGrid : DirGrid2).Focus(FocusState.Programmatic);
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
        var tabs = left ? _leftTabs : _rightTabs;
        int idx = tabs.IndexOf(tab);
        if (idx < 0 || tabs.Count <= 1)
        {
            return;   // 없거나 마지막 하나면 닫지 않음
        }
        bool wasActive = ReferenceEquals(tab, left ? _leftTab : _rightTab);
        tabs.RemoveAt(idx);
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
        bool left = _leftTabs.Contains(tab);
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
        _leftItems.SetPanelFocused(_windowActive && _activeLeft);
        _rightItems.SetPanelFocused(_windowActive && !_activeLeft);
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
            CloseTab(_activeLeft, _activeLeft ? _leftTab : _rightTab);
            e.Handled = true;
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
        var items = left ? _leftItems : _rightItems;

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
        var grid = left ? DirGrid : DirGrid2;

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
