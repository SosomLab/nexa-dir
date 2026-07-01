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
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly Brush TransparentBrush = new SolidColorBrush(Colors.Transparent);

    public NexaPathBar()
    {
        InitializeComponent();
        PART_Breadcrumb.ItemsSource = _segments;
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
    }

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
        if (sender is Border b && b.Tag is PathSegment s && !s.IsCurrent)
        {
            b.Background = HoverBrush;   // 클릭 가능한 세그먼트만 hover 강조
        }
    }

    private void OnSegPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.Background = TransparentBrush;
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
        PART_Scroll.Visibility = Visibility.Collapsed;
        PART_Editor.Visibility = Visibility.Visible;
        PART_Editor.Focus(FocusState.Programmatic);
        PART_Editor.SelectAll();   // 전체 경로 선택
    }

    private void ExitEdit(bool commit)
    {
        var text = PART_Editor.Text;
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
}
