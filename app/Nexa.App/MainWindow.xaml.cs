using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        ShowInteropRoundTrip();
        // 좌/우 패널 모두 파일 목록 표시(초안: 좌=홈, 우=문서).
        LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), _leftItems, DirGrid, DirHeader, PathText);
        LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), _rightItems, DirGrid2, DirHeader2, PathText2);
        UpdateBottomDock();
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
    private void LoadDirectory(string path, ObservableCollection<DirItem> items, NexaFileGrid grid, TextBlock header, TextBlock pathText)
    {
        try
        {
            items.Clear();
            foreach (var it in NativeInterop.ReadDir(path, 0))
            {
                items.Add(it);
            }
            grid.ItemsSource = items;
            pathText.Text = path;
            header.Text = $"{path} — {items.Count}개 항목";
        }
        catch (Exception ex)
        {
            header.Text = $"디렉터리 열거 실패: {ex.Message}";
        }
    }

    /// <summary>
    /// 폴더 행의 디스클로저(▶/▼)를 눌러 인라인으로 펼치거나 접는다.
    /// 펼침: 해당 폴더의 자식을 바로 아래에 depth+1로 삽입. 접힘: 뒤따르는 더 깊은 행을 제거.
    /// </summary>
    private void OnToggleExpand(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not DirItem item || !item.IsDir)
        {
            return;
        }
        e.Handled = true;

        var list = _leftItems.Contains(item) ? _leftItems
                 : _rightItems.Contains(item) ? _rightItems
                 : null;
        if (list is null)
        {
            return;
        }
        int idx = list.IndexOf(item);

        if (item.IsExpanded)
        {
            // 접힘: 이 폴더보다 깊은(자손) 행을 연속으로 제거.
            item.IsExpanded = false;
            while (idx + 1 < list.Count && list[idx + 1].Depth > item.Depth)
            {
                list.RemoveAt(idx + 1);
            }
        }
        else
        {
            // 펼침: 자식을 depth+1로 열거해 바로 아래 삽입(실패는 상태바로 격리).
            try
            {
                int at = idx + 1;
                foreach (var child in NativeInterop.ReadDir(item.FullPath, item.Depth + 1))
                {
                    list.Insert(at++, child);
                }
                item.IsExpanded = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"펼치기 실패: {ex.Message}";
            }
        }
    }

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
