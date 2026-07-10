using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;

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
        // 드래그 중 가장자리 자동 스크롤 타이머(가장자리에 머무는 동안 반복 스크롤). 러버밴드 중에도 공용.
        _autoScroll.Interval = TimeSpan.FromMilliseconds(50);
        _autoScroll.Tick += (_, _) =>
        {
            if (_autoScrollDelta == 0)
            {
                return;
            }
            BodyScroll.ChangeView(null, BodyScroll.VerticalOffset + _autoScrollDelta, null, disableAnimation: true);
            if (_marqueeActive)
            {
                UpdateMarquee(_marqueeLastViewport);   // 스크롤로 콘텐츠 좌표가 변함 → 밴드·선택 재계산
            }
        };
        // 러버밴드(마퀴) 다중 선택(B-4) — 행 이벤트가 Handled여도 받도록 handledEventsToo.
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnMarqueePointerMoved), handledEventsToo: true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnMarqueePointerEnd), handledEventsToo: true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(OnMarqueePointerEnd), handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnMarqueePointerEnd), handledEventsToo: true);
        // 헤더 셀(패널별 정렬 상태 래퍼)은 호스트가 Columns를 채운 뒤(생성자 이후) 구성 → Loaded에서 1회 빌드.
        Loaded += (_, _) => BuildHeaderCells();
        // 가로 스크롤 동기(헤더→본문 방향) — 본문→헤더는 OnBodyViewChanged. 오프셋 비교 가드로 루프 방지.
        HeaderScroll.ViewChanged += (_, _) =>
        {
            if (Math.Abs(HeaderScroll.HorizontalOffset - BodyScroll.HorizontalOffset) > 0.5)
            {
                BodyScroll.ChangeView(HeaderScroll.HorizontalOffset, null, null, disableAnimation: true);
            }
        };
    }

    /// <summary>본문 스크롤 변경 → 헤더 가로 오프셋 동기(컬럼이 뷰포트를 넘을 때 헤더·본문이 함께 이동).</summary>
    private void OnBodyViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (Math.Abs(HeaderScroll.HorizontalOffset - BodyScroll.HorizontalOffset) > 0.5)
        {
            HeaderScroll.ChangeView(BodyScroll.HorizontalOffset, null, null, disableAnimation: true);
        }
    }

    // 이 패널(그리드)만의 헤더 셀 — 정렬 표시(▲/▼)가 좌/우 독립(공유 컬럼은 너비만 공유).
    private readonly List<HeaderCell> _headerCells = new();
    private bool _headerBuilt;
    private Windows.UI.Text.FontWeight _headerWeight = Microsoft.UI.Text.FontWeights.SemiBold;
    private Windows.UI.Text.FontStyle _headerStyle = Windows.UI.Text.FontStyle.Normal;

    private void BuildHeaderCells()
    {
        if (_headerBuilt || Columns.Count == 0)
        {
            return;
        }
        foreach (var col in Columns)
        {
            _headerCells.Add(new HeaderCell(col) { TextWeight = _headerWeight, TextStyle = _headerStyle });
        }
        HeaderRepeater.ItemsSource = _headerCells;
        _headerBuilt = true;
    }

    /// <summary>헤더 라벨 꾸미기(굵기/기울임) 적용 — 설정 "파일 헤더 글꼴"(PREF-3). 글꼴/크기는
    /// 컨트롤 FontFamily/FontSize(파일 목록 글꼴)를 그대로 상속하므로 여기선 꾸미기만 바꾼다.</summary>
    public void SetHeaderTextStyle(Windows.UI.Text.FontWeight weight, Windows.UI.Text.FontStyle style)
    {
        _headerWeight = weight;
        _headerStyle = style;
        foreach (var hc in _headerCells)
        {
            hc.TextWeight = weight;
            hc.TextStyle = style;
        }
    }

    // ── 드래그 중 가장자리 자동 스크롤 (B-11) ────────────────────────
    private readonly DispatcherTimer _autoScroll = new();
    private double _autoScrollDelta;

    /// <summary>본문 빈 영역(행이 소비하지 않은 곳)에 드롭됨 — 호스트가 현재 폴더로 이동/복사 처리
    /// (이벤트 인자 전달 — 외부 드롭은 호스트가 <c>DataView</c>/deferral 사용, B-12/B-14dnd/DND-EXT).</summary>
    public event Action<DragEventArgs>? BodyDropped;

    /// <summary>빈 영역 드롭 캡션에 쓸 대상 폴더 표시명(호스트가 현재 폴더 변경 시 갱신). 비면 일반 "복사/이동" 캡션.</summary>
    public string? DropTargetName { get; set; }

    // 드롭 캡션 문구(i18n) — 컨트롤은 로컬라이저를 모름 → 호스트가 주입(기본값=한국어 폴백). {0}=대상 폴더명.
    public string DropCopyCaption { get; set; } = "복사";
    public string DropMoveCaption { get; set; } = "이동";
    public string DropCopyToFormat { get; set; } = "{0}에 복사";
    public string DropMoveToFormat { get; set; } = "{0}(으)로 이동";

    /// <summary>빈 영역 드래그의 연산 결정(호스트가 자기폴더 Move 금지·외부 드래그 등 판단). 미설정 시 금지(None).</summary>
    public Func<DragEventArgs, DataPackageOperation>? BodyDragOperation { get; set; }

    /// <summary>본문 위 드래그 → 자동 스크롤 + <b>빈 영역만</b> 연산/캡션 결정(폴더 행은 호스트 <c>OnRowDragOver</c>가 이미 수락 → 덮어쓰지 않음).</summary>
    private void OnBodyDragOver(object sender, DragEventArgs e)
    {
        // 이벤트는 행→본문으로 버블링. 행 핸들러(호스트)가 폴더를 수락했으면 AcceptedOperation≠None →
        // 그 폴더명 캡션/연산을 유지(덮어쓰면 폴더 위에서도 현재 폴더 캡션이 돼버림). None(파일 행·진짜 빈 영역)일 때만 배경 처리.
        if (e.AcceptedOperation == DataPackageOperation.None)
        {
            var op = BodyDragOperation?.Invoke(e) ?? DataPackageOperation.None;
            e.AcceptedOperation = op;
            if (op == DataPackageOperation.None)
            {
                e.DragUIOverride.IsGlyphVisible = true;      // 금지 글리프(🚫) — 자기 폴더 Move 등 금지의 시각 피드백
                e.DragUIOverride.IsCaptionVisible = false;   // 캡션 없음
            }
            else
            {
                e.DragUIOverride.IsGlyphVisible = false;     // 수락 시엔 캡션이 대신
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
                bool copy = op == DataPackageOperation.Copy;
                string caption = string.IsNullOrEmpty(DropTargetName)
                    ? (copy ? DropCopyCaption : DropMoveCaption)
                    : string.Format(copy ? DropCopyToFormat : DropMoveToFormat, DropTargetName);
                if (e.DragUIOverride.Caption != caption)
                {
                    e.DragUIOverride.Caption = caption;   // 변경 시만 설정(마우스 이동마다 재설정 회피)
                }
            }
        }
        const double edge = 32;   // 가장자리 감지 폭(px)
        const double speed = 20;  // 틱당 스크롤(px)
        var p = e.GetPosition(BodyScroll);
        double h = BodyScroll.ActualHeight;
        _autoScrollDelta = p.Y < edge ? -speed : (p.Y > h - edge ? speed : 0);
        if (_autoScrollDelta != 0)
        {
            if (!_autoScroll.IsEnabled) { _autoScroll.Start(); }
        }
        else
        {
            _autoScroll.Stop();
        }
    }

    private void OnBodyDragLeave(object sender, DragEventArgs e) => StopAutoScroll();

    private void OnBodyDragEnd(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        BodyDropped?.Invoke(e);   // 행이 소비하지 않은 드롭 = 빈 영역 → 현재 폴더로(외부 드롭은 호스트가 DataView 읽음)
    }

    private void StopAutoScroll()
    {
        _autoScrollDelta = 0;
        _autoScroll.Stop();
    }

    /// <summary>드래그 취소/종료 시 자동 스크롤을 강제 정지(호스트가 <c>DropCompleted</c>에서 호출, B-14).</summary>
    public void StopDragAutoScroll() => StopAutoScroll();

    // ── 러버밴드(마퀴) 다중 선택 (B-4) ────────────────────────────────
    // 시작 후보는 호스트가 지정(빈 영역 press·미선택 행 press). 4px 임계 이동 시 활성화(클릭/더블클릭 보존),
    // 활성화되면 포인터 캡처 + 밴드 사각형 표시 + 교차 행의 "연속 인덱스 범위"를 이벤트로 통지(가상화 안전 —
    // 행 높이 균일이라 실체화 여부와 무관하게 인덱스 계산). 가장자리 자동 스크롤은 DnD 타이머 공용.

    /// <summary>밴드에 교차하는 가시 행 범위(first,last — 없으면 -1,-1). 호스트가 선택 모델에 반영.</summary>
    public event Action<int, int>? MarqueeSelect;

    private bool _marqueeCandidate;    // press 접수(임계 이동 전 — 클릭이면 무산)
    private bool _marqueeActive;       // 임계 초과 → 밴드 표시·선택 중
    private uint _marqueePointerId;
    private Windows.Foundation.Point _marqueeOriginContent;   // 시작점(콘텐츠 좌표 — 스크롤 보정)
    private Windows.Foundation.Point _marqueeLastViewport;    // 마지막 포인터(뷰포트 좌표 — 자동 스크롤 재계산용)

    /// <summary>러버밴드 시작 후보 등록 — 좌버튼 press에서 호스트가 호출(빈 영역·미선택 행).
    /// 실제 시작은 4px 이동 후(단순 클릭·더블클릭은 밴드 없이 끝남).</summary>
    public void StartMarqueeCandidate(PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(BodyScroll);
        if (!pt.Properties.IsLeftButtonPressed)
        {
            return;
        }
        _marqueeCandidate = true;
        _marqueeActive = false;
        _marqueePointerId = e.Pointer.PointerId;
        _marqueeOriginContent = new Windows.Foundation.Point(
            pt.Position.X + BodyScroll.HorizontalOffset,   // 가로 스크롤 보정(콘텐츠 좌표)
            pt.Position.Y + BodyScroll.VerticalOffset);
    }

    /// <summary>행(콘텐츠) 총폭 — 컬럼 너비 합. 밴드가 이 폭과 겹칠 때만 행 선택(탐색기 details 동일).</summary>
    private double RowContentWidth()
    {
        double w = 0;
        foreach (var c in Columns)
        {
            w += c.Width;
        }
        return w > 0 ? w : double.MaxValue;   // 컬럼 미구성 그리드는 전폭 취급
    }

    private void OnMarqueePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_marqueeCandidate || e.Pointer.PointerId != _marqueePointerId)
        {
            return;
        }
        var pt = e.GetCurrentPoint(BodyScroll);
        if (!pt.Properties.IsLeftButtonPressed)
        {
            EndMarquee();
            return;
        }
        var vp = pt.Position;
        if (!_marqueeActive)
        {
            // 4px 임계 — 클릭/더블클릭 제스처 보존(임계 전엔 캡처하지 않아 행 이벤트 흐름 유지).
            double dx = (vp.X + BodyScroll.HorizontalOffset) - _marqueeOriginContent.X;
            double dy = (vp.Y + BodyScroll.VerticalOffset) - _marqueeOriginContent.Y;
            if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4)
            {
                return;
            }
            _marqueeActive = true;
            CapturePointer(e.Pointer);
            MarqueeRect.Visibility = Visibility.Visible;
        }
        _marqueeLastViewport = vp;
        UpdateMarquee(vp);
        // 가장자리 자동 스크롤(DnD와 동일 파라미터·타이머).
        const double edge = 32;
        const double speed = 20;
        double h = BodyScroll.ActualHeight;
        _autoScrollDelta = vp.Y < edge ? -speed : (vp.Y > h - edge ? speed : 0);
        if (_autoScrollDelta != 0)
        {
            if (!_autoScroll.IsEnabled) { _autoScroll.Start(); }
        }
        else
        {
            _autoScroll.Stop();
        }
    }

    /// <summary>밴드 사각형(뷰포트 좌표로 클램프해 그림) + 교차 행 범위 통지.</summary>
    private void UpdateMarquee(Windows.Foundation.Point viewport)
    {
        double offset = BodyScroll.VerticalOffset;
        double hOffset = BodyScroll.HorizontalOffset;
        double curX = viewport.X + hOffset;
        double curYContent = viewport.Y + offset;

        // 콘텐츠 좌표의 밴드.
        double x1 = Math.Min(_marqueeOriginContent.X, curX);
        double x2 = Math.Max(_marqueeOriginContent.X, curX);
        double y1 = Math.Min(_marqueeOriginContent.Y, curYContent);
        double y2 = Math.Max(_marqueeOriginContent.Y, curYContent);

        // 시각(뷰포트 좌표, 본문 영역으로 클램프).
        double vy1 = Math.Max(0, y1 - offset);
        double vy2 = Math.Min(BodyScroll.ActualHeight, y2 - offset);
        double vx1 = Math.Max(0, x1 - hOffset);
        double vx2 = Math.Min(BodyScroll.ActualWidth, x2 - hOffset);
        Canvas.SetLeft(MarqueeRect, vx1);
        Canvas.SetTop(MarqueeRect, vy1);
        MarqueeRect.Width = Math.Max(0, vx2 - vx1);
        MarqueeRect.Height = Math.Max(0, vy2 - vy1);

        // 교차 행 범위(연속) — 행 폭(컬럼 합)과 x 겹침 필요(크기 컬럼 뒤 빈 공간에서 수직 드래그 = 무선택).
        int count = Repeater.ItemsSourceView?.Count ?? 0;
        double stride = EstimateRowStride();
        int first = -1, last = -1;
        if (count > 0 && x1 <= RowContentWidth() && stride > 0)
        {
            first = Math.Max(0, (int)Math.Floor(y1 / stride));
            last = Math.Min(count - 1, (int)Math.Floor((y2 - 0.1) / stride));
            if (last < first)
            {
                first = last = -1;
            }
        }
        MarqueeSelect?.Invoke(first, last);
    }

    private void OnMarqueePointerEnd(object sender, PointerRoutedEventArgs e) => EndMarquee();

    private void EndMarquee()
    {
        if (!_marqueeCandidate)
        {
            return;
        }
        _marqueeCandidate = false;
        bool wasActive = _marqueeActive;
        _marqueeActive = false;
        MarqueeRect.Visibility = Visibility.Collapsed;
        StopAutoScroll();
        if (wasActive)
        {
            ReleasePointerCaptures();
        }
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
                return fe.ActualHeight - 1;   // Spacing=0 + 행 상단 -1px 겹침(테두리 1px 경계)
            }
        }
        return 19;   // 폴백(아이콘 16 + 패딩 1+1 + 테두리 1+1 − 겹침 1)
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

    // ── 헤더 클릭 정렬 (3상태 순환 + SortRequested) — COL-2c ────────────

    /// <summary>헤더 정렬 요청 — 현재 활성 정렬 서술자 목록(순번 순). 호스트가 코어에 적용(도메인 비종속).</summary>
    public event Action<IReadOnlyList<SortDescriptor>>? SortRequested;

    /// <summary>
    /// 헤더 셀 클릭 → 정렬. 기본은 <b>단일 컬럼 3상태 순환</b>(없음→오름→내림→없음, 나머지 해제).
    /// <b>Shift+클릭 &amp; 이미 정렬된 컬럼이 1개 이상</b>이면 <b>다중열 정렬</b>: 이 컬럼을 정렬 집합에
    /// 추가(순번 부여)하거나, 이미 정렬 키면 방향 순환(없음이면 집합에서 제거·순번 당김). 이 패널만(COL-3).
    /// </summary>
    private void OnHeaderTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not HeaderCell cell || !cell.Column.Sortable)
        {
            return;
        }
        e.Handled = true;
        // 다중열은 Shift + 이미 정렬된 컬럼이 하나라도 있을 때만(요청). 아니면 단일 정렬로 리셋.
        bool multi = IsShiftDown() && _headerCells.Any(h => h.Sort != ColumnSort.None);
        if (multi)
        {
            ApplyMultiSort(cell);
        }
        else
        {
            ApplySingleSort(cell);
        }
        UpdateSortOrderBadges();
        RaiseSortRequested();
    }

    /// <summary>단일 컬럼 정렬(3상태 순환) — 나머지 헤더 셀 해제.</summary>
    private void ApplySingleSort(HeaderCell cell)
    {
        ColumnSort next = Cycle(cell.Sort);
        foreach (var hc in _headerCells)
        {
            if (!ReferenceEquals(hc, cell))
            {
                hc.Sort = ColumnSort.None;
                hc.Order = 0;
            }
        }
        cell.Sort = next;
        cell.Order = next == ColumnSort.None ? 0 : 1;
    }

    /// <summary>다중열 정렬 — 이 컬럼을 정렬 집합에 추가/토글(기존 순번 유지, 제거 시 뒤 순번 당김).</summary>
    private void ApplyMultiSort(HeaderCell cell)
    {
        if (cell.Sort == ColumnSort.None)
        {
            // 새 정렬 키 = 현재 최대 순번 + 1, 오름차순으로 추가.
            cell.Order = _headerCells.Max(h => h.Order) + 1;
            cell.Sort = ColumnSort.Ascending;
            return;
        }
        // 이미 정렬 키 → 방향만 순환(오름→내림→없음). 없음이면 집합에서 제거하고 뒤 순번을 당긴다.
        ColumnSort next = cell.Sort == ColumnSort.Ascending ? ColumnSort.Descending : ColumnSort.None;
        if (next == ColumnSort.None)
        {
            int removedOrder = cell.Order;
            cell.Sort = ColumnSort.None;
            cell.Order = 0;
            foreach (var hc in _headerCells)
            {
                if (hc.Sort != ColumnSort.None && hc.Order > removedOrder)
                {
                    hc.Order--;
                }
            }
        }
        else
        {
            cell.Sort = next;   // 순번 유지
        }
    }

    private static ColumnSort Cycle(ColumnSort s) => s switch
    {
        ColumnSort.None => ColumnSort.Ascending,
        ColumnSort.Ascending => ColumnSort.Descending,
        _ => ColumnSort.None,
    };

    /// <summary>정렬된 각 컬럼의 순번을 <b>원문자</b>(①②③…)로 컬럼명 뒤에 표시(정렬 안 된 컬럼은 빈 문자열).</summary>
    private void UpdateSortOrderBadges()
    {
        foreach (var hc in _headerCells)
        {
            hc.OrderText = hc.Sort != ColumnSort.None ? CircledNumber(hc.Order) : string.Empty;
        }
    }

    /// <summary>1→① … 20→⑳(U+2460~). 범위 밖은 평문 숫자.</summary>
    private static string CircledNumber(int n) =>
        n is >= 1 and <= 20
            ? ((char)('①' + (n - 1))).ToString()
            : n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsShiftDown() =>
        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

    private void RaiseSortRequested()
    {
        var list = new List<SortDescriptor>();
        foreach (var hc in _headerCells)
        {
            if (hc.Sort != ColumnSort.None)
            {
                list.Add(new SortDescriptor(hc.Column.Key, hc.Sort == ColumnSort.Descending, hc.Order));
            }
        }
        list.Sort((a, b) => a.Order.CompareTo(b.Order));
        SortRequested?.Invoke(list);
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
