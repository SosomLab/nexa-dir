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
    // 패널별 평면 표시 목록(인라인 트리: 펼침 시 자식을 이 목록에 삽입/제거).
    private readonly ObservableCollection<DirItem> _leftItems = new();
    private readonly ObservableCollection<DirItem> _rightItems = new();

    // 펼침 상태 유지(F18)는 이제 **탭별**(활성 탭의 Expanded). 경로 대소문자 무시.
    private HashSet<string> _leftExpanded => _leftTab.Expanded;
    private HashSet<string> _rightExpanded => _rightTab.Expanded;

    // 범위 선택(Shift) 기준점(고정 anchor) + 키보드 캐럿(현재 위치) — 패널별.
    private DirItem? _leftAnchor;
    private DirItem? _rightAnchor;
    private DirItem? _leftCaret;
    private DirItem? _rightCaret;

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
        ShowInteropRoundTrip();
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
    /// 목록은 <see cref="ObservableCollection{T}"/> — 펼침/접힘 시 자식을 삽입/제거해 트리를 표현.
    /// </summary>
    private void LoadDirectory(string path, ObservableCollection<DirItem> items, NexaFileGrid grid, TextBlock header, NexaPathBar pathBar)
    {
        try
        {
            items.Clear();
            if (items == _leftItems)
            {
                _leftAnchor = null;
            }
            else
            {
                _rightAnchor = null;
            }
            int direct = 0;
            foreach (var it in NativeInterop.ReadDir(path, 0))
            {
                items.Add(it);
                _ = LoadIconAsync(it);   // 실제 셸 아이콘 비동기 로드(폴백: 글리프)
                direct++;
            }
            grid.ItemsSource = items;
            pathBar.Path = path;
            header.Text = $"{path} — {direct}개 항목";
            // 펼침 상태 유지: 이 폴더로 진입/이동해도 하위 열린 폴더를 동일하게 유지(F18, FR-X4).
            ApplySavedExpansion(items, ExpandedSet(items));
        }
        catch (Exception ex)
        {
            header.Text = $"디렉터리 열거 실패: {ex.Message}";
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
        var list = left ? _leftItems : _rightItems;
        var target = fullPath.TrimEnd('\\', '/');
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].FullPath.TrimEnd('\\', '/'), target, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var it in list)
                {
                    it.IsSelected = false;
                }
                list[i].IsSelected = true;
                if (left) { _leftAnchor = list[i]; } else { _rightAnchor = list[i]; }
                MoveCaret(left, list[i]);
                (left ? DirGrid : DirGrid2).BringIndexIntoView(i);
                UpdateSelectionCount(list);
                RefreshSelectionFocus();   // 선택 항목 포커스 색(파랑) 반영
                return;
            }
        }
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
        SetExpanded(item, !item.IsExpanded);
    }

    /// <summary>경로 비교 정규화(끝 구분자 제거).</summary>
    private static string NormPath(string p) => p.TrimEnd('\\', '/');

    /// <summary>패널 목록에 대응하는 "펼친 폴더" 기억 집합.</summary>
    private HashSet<string> ExpandedSet(ObservableCollection<DirItem> list)
        => ReferenceEquals(list, _leftItems) ? _leftExpanded : _rightExpanded;

    /// <summary>
    /// 폴더 항목을 펼치거나 접는다(디스클로저 클릭·키보드 →/← 공용). 폴더가 아니거나 이미 그 상태면 무시.
    /// 펼침 시 그 폴더의 경로를 기억하고, 하위에 저장된 펼침 상태도 함께 복원한다(F18).
    /// 접힘 시 그 폴더만 기억에서 제거(자손 상태는 유지 → 재펼침 시 복원). 자손 행은 목록에서 제거.
    /// </summary>
    private void SetExpanded(DirItem item, bool expand)
    {
        if (!item.IsDir || item.IsExpanded == expand)
        {
            return;
        }
        var list = _leftItems.Contains(item) ? _leftItems
                 : _rightItems.Contains(item) ? _rightItems
                 : null;
        if (list is null)
        {
            return;
        }
        var set = ExpandedSet(list);
        if (!expand)
        {
            set.Remove(NormPath(item.FullPath));   // 이 폴더는 접힘으로 기억(자손 상태는 유지)
            CollapseInPlace(list, item);
        }
        else
        {
            set.Add(NormPath(item.FullPath));
            ExpandInPlace(list, item);
            ApplySavedExpansion(list, set);        // 하위에 저장된 펼침 상태 복원(재귀)
        }
    }

    /// <summary>폴더 자식을 depth+1로 바로 아래 삽입(펼침). 기억 집합은 건드리지 않음. 실패는 상태바로 격리.</summary>
    private void ExpandInPlace(ObservableCollection<DirItem> list, DirItem item)
    {
        if (item.IsExpanded)
        {
            return;
        }
        try
        {
            int at = list.IndexOf(item) + 1;
            foreach (var child in NativeInterop.ReadDir(item.FullPath, item.Depth + 1))
            {
                list.Insert(at++, child);
                _ = LoadIconAsync(child);
            }
            item.IsExpanded = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"펼치기 실패: {ex.Message}";
        }
    }

    /// <summary>이 폴더보다 깊은(자손) 행을 연속 제거(접힘). 기억 집합은 건드리지 않음.</summary>
    private static void CollapseInPlace(ObservableCollection<DirItem> list, DirItem item)
    {
        item.IsExpanded = false;
        int idx = list.IndexOf(item);
        while (idx + 1 < list.Count && list[idx + 1].Depth > item.Depth)
        {
            list.RemoveAt(idx + 1);
        }
    }

    /// <summary>
    /// 목록을 훑어 기억 집합(<paramref name="set"/>)에 있는 폴더를 펼친다.
    /// 삽입된 자식도 순차로 방문되므로 **임의 깊이까지 재귀 복원**된다(폴더 진입/이동 시 하위 상태 동일 표시, F18).
    /// </summary>
    private void ApplySavedExpansion(ObservableCollection<DirItem> list, HashSet<string> set)
    {
        if (set.Count == 0)
        {
            return;
        }
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it.IsDir && !it.IsExpanded && set.Contains(NormPath(it.FullPath)))
            {
                ExpandInPlace(list, it);
            }
        }
    }

    // ── 파일 선택 (단일 · Ctrl 다중 · Shift 범위) ────────────────────
    // 선택 상태는 DirItem.IsSelected(행 배경). 범위 기준점(anchor)은 패널별.

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not DirItem item)
        {
            return;
        }
        var list = _leftItems.Contains(item) ? _leftItems
                 : _rightItems.Contains(item) ? _rightItems
                 : null;
        if (list is null)
        {
            return;
        }
        bool left = list == _leftItems;
        SetActivePanel(left);   // 클릭한 패널이 활성 → 반대 패널 선택은 회색(포커스아웃)
        (left ? DirGrid : DirGrid2).Focus(FocusState.Programmatic);   // 키보드 이동 대상 포커스
        bool ctrl = IsCtrlDown();
        bool shift = IsShiftDown();
        DirItem? anchor = left ? _leftAnchor : _rightAnchor;

        if (shift && anchor is not null && list.Contains(anchor))
        {
            // 범위 선택: 기준점~클릭 항목(목록 순서). Ctrl 병행 시 기존 선택 유지(추가).
            int a = list.IndexOf(anchor);
            int b = list.IndexOf(item);
            if (a > b)
            {
                (a, b) = (b, a);
            }
            if (!ctrl)
            {
                foreach (var it in list)
                {
                    it.IsSelected = false;
                }
            }
            for (int i = a; i <= b; i++)
            {
                list[i].IsSelected = true;
            }
            // 기준점은 유지(연속 Shift 확장).
        }
        else if (ctrl)
        {
            // 토글 다중 선택.
            item.IsSelected = !item.IsSelected;
            anchor = item;
        }
        else
        {
            // 단일 선택(나머지 해제).
            foreach (var it in list)
            {
                it.IsSelected = false;
            }
            item.IsSelected = true;
            anchor = item;
        }

        if (left)
        {
            _leftAnchor = anchor;
        }
        else
        {
            _rightAnchor = anchor;
        }
        MoveCaret(left, item);   // 키보드 이동이 클릭 지점부터 이어지도록 캐럿 갱신(포커스 외곽선)
        UpdateSelectionCount(list);
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
        if (_leftItems.Contains(item))
        {
            Navigate(true, item.FullPath, record: true);
        }
        else if (_rightItems.Contains(item))
        {
            Navigate(false, item.FullPath, record: true);
        }
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
            if (left) { _leftTab = tab; _leftCaret = null; _leftAnchor = null; }
            else { _rightTab = tab; _rightCaret = null; _rightAnchor = null; }
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
        bool leftFocused = _windowActive && _activeLeft;
        bool rightFocused = _windowActive && !_activeLeft;
        foreach (var it in _leftItems)
        {
            if (it.IsSelected)
            {
                it.PanelFocused = leftFocused;
            }
        }
        foreach (var it in _rightItems)
        {
            if (it.IsSelected)
            {
                it.PanelFocused = rightFocused;
            }
        }
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
    {
        _windowActive = e.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
        RefreshSelectionFocus();
    }

    /// <summary>키보드 캐럿(현재 위치)을 이동하고 포커스 외곽선을 갱신한다(패널별 1개).</summary>
    private void MoveCaret(bool left, DirItem? item)
    {
        var old = left ? _leftCaret : _rightCaret;
        if (old is not null && !ReferenceEquals(old, item))
        {
            old.IsCaret = false;
        }
        if (item is not null)
        {
            item.IsCaret = true;
        }
        if (left)
        {
            _leftCaret = item;
        }
        else
        {
            _rightCaret = item;
        }
    }

    /// <summary>펼친 목록에서 부모 행 인덱스(현재보다 Depth가 1 작은 최근접 상위 행). 없으면 -1(최상위).</summary>
    private static int ParentIndex(IList<DirItem> list, int index)
    {
        if (index < 0)
        {
            return -1;
        }
        int depth = list[index].Depth;
        if (depth <= 0)
        {
            return -1; // 목록 최상위(부모 행이 목록에 없음)
        }
        for (int i = index - 1; i >= 0; i--)
        {
            if (list[i].Depth == depth - 1)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>선택 개수를 세어 상태바에 표시.</summary>
    private void UpdateSelectionCount(IEnumerable<DirItem> list)
    {
        int count = 0;
        foreach (var it in list)
        {
            if (it.IsSelected)
            {
                count++;
            }
        }
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
        var list = left ? _leftItems : _rightItems;

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
                    var altCaret = left ? _leftCaret : _rightCaret;
                    int altCur = altCaret is not null ? list.IndexOf(altCaret) : -1;
                    if (altCur >= 0) { ActivateItem(left, list[altCur]); }
                    e.Handled = true;
                    return;
                }
            }
        }

        if (list.Count == 0)
        {
            return;   // 여기서부터는 목록 항목이 필요한 동작(선택 이동/펼침/Space)
        }
        SetActivePanel(left);
        var caret = left ? _leftCaret : _rightCaret;
        int cur = caret is not null ? list.IndexOf(caret) : -1;
        bool ctrl = IsCtrlDown();
        bool shift = IsShiftDown();

        if (space)
        {
            // 캐럿 항목 선택. Ctrl+Space=토글(비연속 다중, 나머지 유지), Space=단일 선택.
            if (cur >= 0)
            {
                var item = list[cur];
                if (ctrl)
                {
                    item.IsSelected = !item.IsSelected;
                }
                else
                {
                    foreach (var it in list)
                    {
                        it.IsSelected = false;
                    }
                    item.IsSelected = true;
                }
                if (left) { _leftAnchor = item; } else { _rightAnchor = item; }
                UpdateSelectionCount(list);
            }
            e.Handled = true;
            return;
        }

        if (horizontal)
        {
            if (e.Key == VirtualKey.Right)
            {
                // →: 현재 폴더 펼침(폴더가 아니면 무시).
                if (cur >= 0 && list[cur].IsDir)
                {
                    SetExpanded(list[cur], expand: true);
                }
            }
            else if (cur >= 0)
            {
                // ←: 펼쳐진 폴더면 접기. 접힌 폴더/파일이면 **상위(부모) 폴더로 이동**(단일 선택).
                //     목록 최상위(부모 행 없음)면 아무 동작 없음.
                if (list[cur].IsDir && list[cur].IsExpanded)
                {
                    SetExpanded(list[cur], expand: false);
                }
                else
                {
                    int p = ParentIndex(list, cur);
                    if (p >= 0)
                    {
                        foreach (var it in list)
                        {
                            it.IsSelected = false;
                        }
                        list[p].IsSelected = true;
                        if (left) { _leftAnchor = list[p]; } else { _rightAnchor = list[p]; }
                        MoveCaret(left, list[p]);
                        (left ? DirGrid : DirGrid2).BringIndexIntoView(p);
                        UpdateSelectionCount(list);
                    }
                }
            }
            e.Handled = true;
            return;
        }

        // 세로 이동(캐럿 기준 한 칸).
        int next = e.Key == VirtualKey.Down ? cur + 1 : cur - 1;
        if (next < 0)
        {
            next = 0;
        }
        if (next >= list.Count)
        {
            next = list.Count - 1;
        }

        if (ctrl && !shift)
        {
            // 비연속 다중 선택 모드: 선택은 그대로 두고 캐럿(위치)만 이동. Space로 개별 토글.
            MoveCaret(left, list[next]);
            (left ? DirGrid : DirGrid2).BringIndexIntoView(next);
            e.Handled = true;
            return;
        }

        var anchor = left ? _leftAnchor : _rightAnchor;
        if (shift)
        {
            // 범위 확장: 고정 anchor ~ 새 캐럿(next) 선택.
            if (anchor is null || !list.Contains(anchor))
            {
                anchor = cur >= 0 ? list[cur] : list[next];
            }
            int a = list.IndexOf(anchor);
            int b = next;
            if (a > b)
            {
                (a, b) = (b, a);
            }
            foreach (var it in list)
            {
                it.IsSelected = false;
            }
            for (int i = a; i <= b; i++)
            {
                list[i].IsSelected = true;
            }
            // anchor 유지(연속 Shift 확장), 캐럿만 이동.
        }
        else
        {
            // 단일 선택 이동: anchor=caret=next.
            foreach (var it in list)
            {
                it.IsSelected = false;
            }
            list[next].IsSelected = true;
            anchor = list[next];
        }

        if (left) { _leftAnchor = anchor; } else { _rightAnchor = anchor; }
        MoveCaret(left, list[next]);
        (left ? DirGrid : DirGrid2).BringIndexIntoView(next);
        UpdateSelectionCount(list);
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
