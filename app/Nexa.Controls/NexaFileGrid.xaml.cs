using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Nexa.Controls;

/// <summary>
/// 가상화 파일 목록/트리 컨트롤(도메인 비종속). <c>ItemsRepeater</c>를 래핑하고 행 표현은
/// <see cref="ItemTemplate"/>로 주입받는다 — 파일/검색/클라우드/플러그인 뷰가 재사용(ADR-0002 §9).
/// 후속: 컬럼 헤더·리사이즈(<c>IColumn</c>/<c>ICellValueProvider</c>), 트리 depth·펼침.
/// </summary>
public sealed partial class NexaFileGrid : UserControl
{
    public NexaFileGrid()
    {
        InitializeComponent();
    }

    /// <summary>컬럼 정의(헤더 행). XAML에서 채우고, 본문 셀은 <see cref="ItemTemplate"/>이 렌더.</summary>
    public IList<NexaGridColumn> Columns { get; } = new List<NexaGridColumn>();

    // ── 컬럼 리사이즈 (헤더 우측 핸들 드래그, PointerMove + 포인터 캡처) ──
    private NexaGridColumn? _resizingCol;
    private double _resizeStartX;
    private double _resizeStartWidth;

    private void OnResizeStart(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is NexaGridColumn col)
        {
            _resizingCol = col;
            _resizeStartX = e.GetCurrentPoint(this).Position.X;
            _resizeStartWidth = col.Width;
            fe.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnResizeMove(object sender, PointerRoutedEventArgs e)
    {
        if (_resizingCol is not null && sender is FrameworkElement fe && fe.PointerCaptures is { Count: > 0 })
        {
            double x = e.GetCurrentPoint(this).Position.X;
            _resizingCol.Width = Math.Max(40, _resizeStartWidth + (x - _resizeStartX));
            e.Handled = true;
        }
    }

    private void OnResizeEnd(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.ReleasePointerCapture(e.Pointer);
        }
        _resizingCol = null;
    }

    /// <summary>행 데이터 컬렉션. 내부 <c>ItemsRepeater.ItemsSource</c>로 전달(가상화).</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource), typeof(object), typeof(NexaFileGrid),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NexaFileGrid)d).Repeater.ItemsSource = e.NewValue;

    /// <summary>
    /// 헤더 행 배경 브러시(본문과 구분). 기본은 슬레이트 톤이며 <b>설정에서 변경 가능</b>하도록
    /// 의존성 속성으로 노출한다(앱/설정이 바인딩·오버라이드). 후속: HeaderForeground 등.
    /// </summary>
    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(
            nameof(HeaderBackground), typeof(Brush), typeof(NexaFileGrid),
            new PropertyMetadata(new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x80, 0x84, 0x88))));

    public Brush HeaderBackground
    {
        get => (Brush)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>행 표현 템플릿(도메인 측에서 주입). 컨트롤은 행 의미를 모른다.</summary>
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate), typeof(DataTemplate), typeof(NexaFileGrid),
            new PropertyMetadata(null));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }
}
