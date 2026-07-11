using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace Nexa.Controls;

/// <summary><see cref="NexaPathBar.Navigated"/> 인자 — 이동 대상 경로.</summary>
public sealed class NexaPathBarNavigatedEventArgs : EventArgs
{
    public string Path { get; }
    public NexaPathBarNavigatedEventArgs(string path) => Path = path;
}

/// <summary>
/// 계층 경로 바(브레드크럼 ↔ 텍스트 편집). 세그먼트 클릭/우클릭 편집을 처리하되
/// **이동은 하지 않고** <see cref="Navigated"/>만 raise한다(호스트가 실제 네비게이션 수행) — 재사용/테스트 용이.
/// 설계: docs/27. α = 로컬 FS 세그먼테이션.
/// </summary>
public sealed partial class NexaPathBar : UserControl
{
    private readonly ObservableCollection<PathSegment> _segments = new();
    // hover 반전: 밝은 배경 + 어두운 글자(다크 UI에서 확연히 튐).
    private static readonly Brush HoverBg = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xEA, 0xEA));
    private static readonly Brush HoverFg = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
    private static readonly Brush TransparentBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    public NexaPathBar()
    {
        InitializeComponent();
        PART_Breadcrumb.ItemsSource = _segments;
        // 창 리사이즈 등으로 폭이 줄면 다시 끝(최근 경로)이 보이게 유지.
        PART_Scroll.SizeChanged += (_, _) => ScrollToEnd();
    }

    /// <summary>표시할 전체 경로. 변경 시 브레드크럼을 다시 만든다.</summary>
    public static readonly DependencyProperty PathProperty =
        DependencyProperty.Register(nameof(Path), typeof(string), typeof(NexaPathBar),
            new PropertyMetadata(string.Empty, OnPathChanged));

    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>세그먼트 클릭 또는 편집 제출로 이동 요청 시 발생(호스트가 실제 이동 수행).</summary>
    public event EventHandler<NexaPathBarNavigatedEventArgs>? Navigated;

    /// <summary>편집 텍스트 → 폴더 제안 목록(전체 경로) 공급자. 호스트가 주입(환경변수 해석·FS 열거 —
    /// 컨트롤은 도메인/IO 비종속 유지). null이면 제안 기능 꺼짐.</summary>
    public Func<string, IReadOnlyList<string>>? SuggestionProvider { get; set; }

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NexaPathBar)d).Rebuild();

    private void Rebuild()
    {
        if (PART_Editor.Visibility == Visibility.Visible)
        {
            return;   // 편집 중이면 유지(제출/취소 시 갱신)
        }
        _segments.Clear();
        var list = BuildSegments(Path);
        if (list.Count > 0)
        {
            list[list.Count - 1].IsCurrent = true;
        }
        foreach (var s in list)
        {
            _segments.Add(s);
        }
        ScrollToEnd();   // 경로가 폭을 넘으면 최근(가장 깊은) 세그먼트가 보이게 — 오른쪽 정렬 효과
    }

    /// <summary>브레드크럼을 끝(최근 경로)으로 스크롤 — 긴 경로는 왼쪽(루트 쪽)이 잘리고 현재 폴더가 보인다.
    /// 레이아웃 확정 후 오프셋이 유효하므로 enqueue. 폭 변경(창 리사이즈) 시에도 유지.</summary>
    private void ScrollToEnd() => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
    {
        PART_Scroll.UpdateLayout();
        if (PART_Scroll.ScrollableWidth > 0)
        {
            PART_Scroll.ChangeView(PART_Scroll.ScrollableWidth, null, null, disableAnimation: true);
        }
    });

    /// <summary>로컬 FS 경로를 세그먼트로 분해(드라이브 "C:" → "C:\\"). UNC/VFS는 후속(docs/27 β/γ).</summary>
    private static List<PathSegment> BuildSegments(string path)
    {
        var list = new List<PathSegment>();
        if (string.IsNullOrEmpty(path))
        {
            return list;
        }
        var trimmed = path.Replace('/', '\\').TrimEnd('\\');
        var parts = trimmed.Split('\\');
        string acc = string.Empty;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (i == 0)
            {
                acc = p;
                var full = p.EndsWith(":", StringComparison.Ordinal) ? p + "\\" : p;
                list.Add(new PathSegment { Prefix = string.Empty, Label = p, FullPath = full });
            }
            else
            {
                acc = acc + "\\" + p;
                list.Add(new PathSegment { Prefix = "\\", Label = p, FullPath = acc });
            }
        }
        return list;
    }

    private void RaiseNavigated(string path) => Navigated?.Invoke(this, new NexaPathBarNavigatedEventArgs(path));

    // ── 브레드크럼 상호작용 ──────────────────────────────────────
    private void OnSegPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // 클릭 가능한 세그먼트만 반전 강조(배경/글자색 뒤집기).
        if (sender is Border b && b.Tag is PathSegment s && !s.IsCurrent)
        {
            b.Background = HoverBg;
            if (b.Child is TextBlock tb)
            {
                tb.Foreground = HoverFg;
            }
        }
    }

    private void OnSegPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.Background = TransparentBrush;
            if (b.Child is TextBlock tb)
            {
                tb.ClearValue(TextBlock.ForegroundProperty);   // 기본(상속) 글자색 복귀
            }
        }
    }

    private void OnSegTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border b && b.Tag is PathSegment s && !s.IsCurrent)
        {
            RaiseNavigated(s.FullPath);
            e.Handled = true;
        }
    }

    // ── 편집 모드 ────────────────────────────────────────────────
    private void OnBreadcrumbRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        EnterEdit();
        e.Handled = true;
    }

    private void EnterEdit()
    {
        PART_Editor.Text = Path;
        // 브레드크럼 TextBlock은 컨트롤 폰트(FontFamily/FontSize)를 상속하지만 TextBox는 테마 기본(14px)을
        // 유지 → 편집기에 컨트롤 폰트를 명시 복사(경로 글꼴 설정과 동일 크기·글꼴).
        PART_Editor.FontFamily = FontFamily;
        PART_Editor.FontSize = FontSize;
        // 편집기(TextBox)의 자연 높이가 브레드크럼보다 커 진입 시 바 높이가 점프하던 문제 →
        // 브레드크럼 실측 높이로 고정(경로 글꼴 설정에 따라 달라지므로 상수 대신 실측).
        if (PART_Scroll.ActualHeight > 0)
        {
            PART_Editor.Height = PART_Scroll.ActualHeight;
        }
        PART_Scroll.Visibility = Visibility.Collapsed;
        PART_Editor.Visibility = Visibility.Visible;
        PART_Editor.Focus(FocusState.Programmatic);
        PART_Editor.SelectAll();   // 전체 경로 선택
    }

    private void ExitEdit(bool commit)
    {
        var text = PART_Editor.Text;
        CloseSuggest();
        PART_Editor.Visibility = Visibility.Collapsed;
        PART_Scroll.Visibility = Visibility.Visible;
        if (commit && !string.IsNullOrWhiteSpace(text) && text.Trim() != Path)
        {
            RaiseNavigated(text.Trim());   // 이동은 호스트가; 성공 시 Path 갱신→Rebuild
        }
        else
        {
            Rebuild();   // 취소/무변경: 브레드크럼 원복
        }
    }

    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 제안 열림 중: ↑/↓=항목 이동(편집기에 미리 채움), ESC=제안만 닫기(편집 유지) — 탐색기 동일.
        if (PART_SuggestPopup.IsOpen)
        {
            if (e.Key is VirtualKey.Down or VirtualKey.Up)
            {
                MoveSuggestSelection(e.Key == VirtualKey.Down ? +1 : -1);
                e.Handled = true;
                return;
            }
            if (e.Key == VirtualKey.Escape)
            {
                CloseSuggest();
                e.Handled = true;
                return;
            }
        }
        if (e.Key == VirtualKey.Enter)
        {
            ExitEdit(commit: true);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ExitEdit(commit: false);
            e.Handled = true;
        }
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (PART_Editor.Visibility == Visibility.Visible)
        {
            ExitEdit(commit: false);   // 포커스아웃 = 입력 무시·복귀
        }
    }

    // ── 폴더 제안 드롭다운(탐색기식 주소 자동완성) ─────────────────────
    private bool _suppressSuggest;   // 제안 항목을 편집기에 채울 때 TextChanged 재발화로 목록이 리셋되는 것 방지

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSuggest || PART_Editor.Visibility != Visibility.Visible || SuggestionProvider is null)
        {
            return;
        }
        IReadOnlyList<string> items;
        try
        {
            items = SuggestionProvider(PART_Editor.Text) ?? Array.Empty<string>();
        }
        catch
        {
            items = Array.Empty<string>();   // 공급자 실패 격리 — 제안만 조용히 생략
        }
        if (items.Count == 0)
        {
            CloseSuggest();
            return;
        }
        PART_SuggestList.ItemsSource = items;
        PART_SuggestList.SelectedIndex = -1;
        // 편집기 바로 아래·같은 폭(탐색기식). 오프셋은 팝업 부모(그리드) 기준.
        PART_SuggestList.Width = Math.Max(120, PART_Editor.ActualWidth - 6);
        PART_SuggestPopup.HorizontalOffset = 0;
        PART_SuggestPopup.VerticalOffset = PART_Editor.ActualHeight + 2;
        PART_SuggestPopup.IsOpen = true;
    }

    /// <summary>↑/↓로 제안 선택 이동 + 선택 경로를 편집기에 미리 채움(캐럿 끝) — Enter로 그대로 이동.</summary>
    private void MoveSuggestSelection(int delta)
    {
        int n = PART_SuggestList.Items.Count;
        if (n == 0)
        {
            return;
        }
        int i = PART_SuggestList.SelectedIndex;
        i = i < 0 ? (delta > 0 ? 0 : n - 1) : Math.Clamp(i + delta, 0, n - 1);
        PART_SuggestList.SelectedIndex = i;
        PART_SuggestList.ScrollIntoView(PART_SuggestList.Items[i]);
        if (PART_SuggestList.Items[i] is string path)
        {
            _suppressSuggest = true;
            PART_Editor.Text = path;
            PART_Editor.SelectionStart = path.Length;   // 캐럿 끝(계속 타이핑 가능)
            _suppressSuggest = false;
        }
    }

    /// <summary>제안 클릭 = 그 폴더로 즉시 이동(탐색기 동일). AllowFocusOnInteraction=False라 편집 포커스 유지 상태에서 처리.</summary>
    private void OnSuggestItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string path)
        {
            _suppressSuggest = true;
            PART_Editor.Text = path;
            _suppressSuggest = false;
            ExitEdit(commit: true);
        }
    }

    private void CloseSuggest()
    {
        PART_SuggestPopup.IsOpen = false;
        PART_SuggestList.ItemsSource = null;
    }
}
