using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nexa.Controls;

/// <summary>
/// 가상화 목록/트리 컨트롤(도메인 비종속). <c>ItemsRepeater</c>를 래핑하고 행 표현은
/// <see cref="ItemTemplate"/>로 주입받는다 — 파일/검색/클라우드/플러그인 뷰가 재사용(ADR-0002 §9).
/// 후속: 컬럼 헤더·리사이즈(<c>IColumn</c>/<c>ICellValueProvider</c>), 트리 depth·펼침.
/// </summary>
public sealed partial class VirtualizedTreeGrid : UserControl
{
    public VirtualizedTreeGrid()
    {
        InitializeComponent();
    }

    /// <summary>행 데이터 컬렉션. 내부 <c>ItemsRepeater.ItemsSource</c>로 전달(가상화).</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource), typeof(object), typeof(VirtualizedTreeGrid),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((VirtualizedTreeGrid)d).Repeater.ItemsSource = e.NewValue;

    /// <summary>행 표현 템플릿(도메인 측에서 주입). 컨트롤은 행 의미를 모른다.</summary>
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate), typeof(DataTemplate), typeof(VirtualizedTreeGrid),
            new PropertyMetadata(null));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }
}
