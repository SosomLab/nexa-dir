using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
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
    // 실체화된 요소 → 그 요소가 표시 중인 데이터. ItemsRepeater + x:Bind 템플릿은 요소 DataContext를
    // 설정하지 않으므로, 진입 시 인덱스로 데이터를 얻어 매핑해 두고 재활용 시 되찾는다(취소 대상 식별).
    private readonly Dictionary<UIElement, object> _rowItem = new();

    public NexaFileGrid()
    {
        InitializeComponent();
        // 행 요소 수명(화면 진입/이탈)을 호스트에 노출 — 아이콘 지연 로드/취소를 뷰포트에 묶어
        // 빠른 스크롤 시 지나간 행의 작업을 취소하게 한다(감사 P6). 도메인 비종속(item은 object).
        Repeater.ElementPrepared += (_, e) =>
        {
            object? item = Repeater.ItemsSourceView?.GetAt(e.Index);
            if (item is not null)
            {
                _rowItem[e.Element] = item;
                RowRealized?.Invoke(item);
            }
        };
        Repeater.ElementClearing += (_, e) =>
        {
            if (_rowItem.Remove(e.Element, out var item))
            {
                RowRecycled?.Invoke(item);
            }
        };
    }

    /// <summary>행 요소가 화면에 실체화될 때 그 데이터로 호출(아이콘 지연 로드 등). 호스트가 구독.</summary>
    public event Action<object>? RowRealized;

    /// <summary>행 요소가 재활용(화면 이탈)될 때 그 데이터로 호출(지연 로드 취소 등). 호스트가 구독.</summary>
    public event Action<object>? RowRecycled;

    /// <summary>컬럼 정의(헤더 행). XAML에서 채우고, 본문 셀은 <see cref="ItemTemplate"/>이 렌더.</summary>
    public IList<NexaGridColumn> Columns { get; } = new List<NexaGridColumn>();

    /// <summary>지정 인덱스 행이 실체화돼 있으면 화면에 보이도록 스크롤(키보드 이동용, 최소 스크롤). 미실체화면 무시.</summary>
    /// <summary>
    /// 지정 인덱스 행이 뷰포트 안에 보이도록 <b>최소 스크롤</b>한다(이미 보이면 무동작). 캐럿(↑↓/Space) 이동용.
    /// <para>오프스크린 행은 <c>Repeater.TryGetElement</c>가 null이라 <c>StartBringIntoView</c>로는 스크롤이
    /// 안 된다(먼 캐럿이 화면 밖으로 사라짐). 행 높이가 균일하므로 실측 stride로 목표 오프셋을 계산해
    /// <c>ChangeView</c>한다 — 대상이 뷰포트 위면 상단, 아래면 하단에 맞추고, 안에 있으면 그대로 둔다
    /// (캐럿이 화면 안일 때 불필요한 스크롤 튐 없음).</para>
    /// </summary>
    public void BringIndexIntoView(int index)
    {
        if (index < 0)
        {
            return;
        }
        double stride = EstimateRowStride();
        double top = index * stride;
        double bottom = top + stride;
        double viewTop = BodyScroll.VerticalOffset;
        double viewport = BodyScroll.ViewportHeight;
        if (top < viewTop)
        {
            BodyScroll.ChangeView(null, top, null, disableAnimation: true);   // 위로: 대상 상단 맞춤
        }
        else if (bottom > viewTop + viewport)
        {
            BodyScroll.ChangeView(null, bottom - viewport, null, disableAnimation: true);   // 아래로: 대상 하단 맞춤
        }
        // 이미 뷰포트 안 → 무동작
    }

    /// <summary>본문 스크롤을 맨 위로 리셋(폴더 진입 시 첫 항목이 위에 오도록).</summary>
    public void ScrollToTop() => BodyScroll.ChangeView(null, 0, null, disableAnimation: true);

    /// <summary>지정 인덱스의 실체화된 행 요소(미실체화면 null). 인라인 편집 등 행 요소 접근용.</summary>
    public FrameworkElement? RowElement(int index) => Repeater.TryGetElement(index) as FrameworkElement;

    /// <summary>현재 본문 세로 스크롤 오프셋(px). 펼침/접힘 전 캡처용.</summary>
    public double VerticalOffset => BodyScroll.VerticalOffset;

    /// <summary>
    /// 세로 스크롤 오프셋을 <paramref name="offset"/>로 복원한다(펼침/접힘의 Reset 후 위치 유지).
    /// 펼침/접힘은 토글 행 <b>아래</b>만 바뀌므로 같은 오프셋을 되돌리면 위쪽 행이 제자리에 남는다.
    /// Reset이 재레이아웃하며 오프셋을 0으로 되돌리므로, 그 뒤에 확정되도록 <c>DispatcherQueue</c>로 지연.
    /// </summary>
    public void RestoreVerticalOffset(double offset)
    {
        if (offset <= 0)
        {
            return;   // 이미 맨 위 — Reset의 기본(0)과 동일, 복원 불필요
        }
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            BodyScroll.ChangeView(null, offset, null, disableAnimation: true));
    }

    /// <summary>
    /// 지정 인덱스 행을 화면에 스크롤한다. <paramref name="verticalAlignmentRatio"/> 0=맨 위, 0.5=가운데, 1=맨 아래.
    /// <para>ItemsRepeater에서 <c>GetOrCreateElement</c>+<c>StartBringIntoView</c>로 먼 인덱스를 강제
    /// 실체화하면 실체화 창과 스크롤 오프셋이 어긋나 <b>상단 공백</b>이 남는다. 이 그리드는 행 높이가
    /// 균일하므로, **실측 행 높이로 목표 오프셋을 계산해 <c>ScrollViewer.ChangeView</c>로 정상 스크롤**한다.</para>
    /// <para><b>비동기 로드/Reset 직후</b>엔 <c>ItemsRepeater</c>가 아직 새 목록을 레이아웃하지 않아
    /// 스크롤 가능 익스텐트(<c>ScrollableHeight</c>)가 목표에 못 미친다 → <c>ChangeView</c>가 상단/하단으로
    /// 클램프돼 대상이 가운데에 안 온다(레이아웃 진행도에 따라 위치가 들쭉날쭉). 익스텐트가 목표를 담을 만큼
    /// 자랄 때까지 **레이아웃 패스마다 현재 최대치로 밀어 가상화 확장을 유도하며 재시도**한다(익스텐트가 더
    /// 안 자라거나 상한 도달 시 확정).</para>
    /// </summary>
    public void ScrollIndexIntoView(int index, double verticalAlignmentRatio)
    {
        if (index < 0)
        {
            return;
        }
        ScrollToIndexWhenReady(index, verticalAlignmentRatio, attempt: 0, prevMax: -1);
    }

    private void ScrollToIndexWhenReady(int index, double ratio, int attempt, double prevMax)
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            var view = Repeater.ItemsSourceView;
            if (view is null || index >= view.Count)
            {
                return;   // 그새 목록이 바뀌었으면 무시
            }
            // 핵심: 비동기 로드/Reset 직후엔 ItemsRepeater가 아직 새 목록을 레이아웃하지 않아 스크롤 익스텐트가
            // 0이다(→ ChangeView가 0으로 클램프돼 최상단에 걸림). 레이아웃을 동기 강제해 가상화 익스텐트를
            // 즉시 확정한다(동기 경로가 성공하던 그 상태를 재현). StackLayout이 (전체 개수×평균 높이)로 추정.
            BodyScroll.UpdateLayout();
            double stride = EstimateRowStride();
            // 대상 행 상단이 (뷰포트 - 행) * ratio 위치에 오도록 오프셋 계산(균일 높이 가정). 0 이상으로 클램프.
            double target = Math.Max(0, index * stride - (BodyScroll.ViewportHeight - stride) * ratio);
            double max = BodyScroll.ScrollableHeight;
            BodyScroll.ChangeView(null, target, null, disableAnimation: true);
            // 안전망: 익스텐트가 아직 목표에 못 미쳐 클램프됐고(레이아웃이 여러 패스 필요) 계속 자라는 중이면 재시도.
            // (끝 근처 항목이라 상한이 고정되면 중단 — 상한 클램프가 최선, 화면 하단에 완전히 표시.)
            if (target > max + 1 && attempt < 10 && (attempt == 0 || max > prevMax + 1))
            {
                ScrollToIndexWhenReady(index, ratio, attempt + 1, max);
            }
        });
    }

    /// <summary>실체화된 행에서 행 간 간격(높이+Spacing)을 실측(없으면 기본값).</summary>
    private double EstimateRowStride()
    {
        for (int i = 0; i < 8; i++)
        {
            if (Repeater.TryGetElement(i) is FrameworkElement fe && fe.ActualHeight > 0)
            {
                return fe.ActualHeight + 2;   // StackLayout Spacing=2
            }
        }
        return 24;   // 폴백(아이콘 16 + 패딩 6 + 간격 2)
    }

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
