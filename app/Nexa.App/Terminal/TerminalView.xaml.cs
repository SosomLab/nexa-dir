using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Nexa.App.Terminal;

/// <summary>
/// 터미널 뷰(BP-T2) — <see cref="ConPtySession"/> 출력을 <see cref="VtScreen"/>(VT 파서+화면 버퍼)에 넣고
/// <b>색 셀 그리드</b>로 렌더한다. 라인마다 같은 스타일 셀을 run으로 묶어 <c>Border</c>(배경)+<c>TextBlock</c>(전경)으로
/// 그린다(oh-my-posh 배경색·ls 색·커서 지우기 반영). 렌더는 throttle(코얼레싱). 세션은 <see cref="Start"/> 시에만(lazy).
/// <para>글리프: 파워라인/Nerd Font 아이콘은 기본 폰트(Consolas)에 없어 네모로 보일 수 있다(색은 정상)
/// — 설정 "콘솔 글꼴"(PREF-3)에서 Nerd Font 등을 쉼표 폴백으로 지정하면 해결.</para>
/// </summary>
public sealed partial class TerminalView : UserControl
{
    // 콘솔 글꼴(설정 PREF-3) — ApplyFont()가 AppSettings.Fonts.Console*로 갱신. 쉼표 목록=합성 폰트 폴백.
    private FontFamily _fontFamily = new("Consolas");
    private double _fontSize = 13;
    private double _charW = 13 * 0.55;    // 셀 폭 — ApplyFont()에서 '0' 20자 실측(고정폭 가정)
    private double _lineH = 13 * 1.35;

    private const int RenderCap = 400;                // 렌더할 최근 라인 상한(성능)

    private const double BlinkMs = 530;               // 캐럿 깜빡임 주기(포커스 시)

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _renderTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _blinkTimer;
    private readonly Dictionary<uint, SolidColorBrush> _brushes = new();
    // 캐럿: 포커스 시 채운 블록(반투명 accent, 깜빡임) / 비포커스 시 외곽선(중공).
    private readonly SolidColorBrush _caretFill = new(Windows.UI.Color.FromArgb(0xC8, 0x3A, 0x96, 0xDD));
    private ConPtySession? _session;
    private VtScreen? _vt;
    private bool _started;
    private bool _dirty;
    private bool _stopping;         // Stop()로 인한 종료(재시작 금지) vs exit로 인한 종료(재시작) 구분
    private bool _focused;          // 캐럿 스타일(블록/중공) 및 깜빡임 제어
    private bool _caretOn = true;   // 깜빡임 위상(true=보임)

    /// <summary>셸 (재)시작 시 작업 디렉터리를 반환(호스트가 선택 탭 폴더 제공). (재)시작 시점 값만 사용 — 이후 폴더 변경/탭 이동은 무영향.</summary>
    public Func<string?>? WorkingDirectoryProvider { get; set; }

    private string? _pendingInput;   // 세션 시작 전 요청된 입력(cd 등) — 시작 직후 전송

    /// <summary>실행 중인 셸의 작업 디렉터리를 <paramref name="folder"/>로 변경(cd 입력 전송) —
    /// 도구 모음 "터미널 위치 이동". 세션이 아직 없으면 시작 후 전송(pending). cmd는 드라이브 전환에 /d 필요.</summary>
    public void ChangeDirectory(string folder)
    {
        bool cmd = ConPtySession.DefaultShell().StartsWith("cmd", StringComparison.OrdinalIgnoreCase);
        string input = (cmd ? $"cd /d \"{folder}\"" : $"cd \"{folder}\"") + "\r";
        if (_session is not null)
        {
            _session.Write(input);
            FocusSoon();
        }
        else
        {
            _pendingInput = input;   // StartSession 완료 직후 전송
            Start();
        }
    }

    public TerminalView()
    {
        InitializeComponent();
        // 클릭 포커스: 내부 ScrollViewer가 포인터 이벤트를 Handled 처리해도 잡히도록 handledEventsToo=true로 등록.
        // (XAML 속성 핸들러는 Handled된 이벤트를 못 받음 → 클릭이 무시되던 원인.)
        // Pressed뿐 아니라 Released도 처리 — WinUI가 pointer release 기본 처리에서 포커스를 다시 뺏어가,
        // "누르는 동안은 입력되다 놓으면 안 되던" 증상. release 후 FocusSoon(enqueue)로 다시 포커스.
        // Moved는 마우스 드래그 선택(복사) 갱신용.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnTermPointerPressed), handledEventsToo: true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnTermPointerMoved), handledEventsToo: true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnTermPointerReleased), handledEventsToo: true);
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _renderTimer = _dispatcher.CreateTimer();
        _renderTimer.Interval = TimeSpan.FromMilliseconds(33);   // ~30fps 코얼레싱
        _renderTimer.IsRepeating = false;
        _renderTimer.Tick += (_, _) => { if (_dirty) { _dirty = false; RenderScreen(); } };
        _blinkTimer = _dispatcher.CreateTimer();
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(BlinkMs);
        _blinkTimer.IsRepeating = true;
        _blinkTimer.Tick += (_, _) => { _caretOn = !_caretOn; UpdateCaret(); };
        SizeChanged += (_, _) => ResizeToView();
        Unloaded += (_, _) => Stop();
        ApplyFont();   // 설정(콘솔 글꼴) 반영 — 세션 시작 전 셀 크기 확정
    }

    /// <summary>설정(콘솔 글꼴, PREF-3)을 반영 — 셀 폭/줄 높이를 실측으로 재계산 후 격자 리사이즈·재렌더.
    /// 쉼표 목록("Cascadia Mono, Consolas")은 WinUI 합성 폰트 문자열로 그대로 폴백된다.</summary>
    public void ApplyFont()
    {
        var f = AppSettings.Fonts;
        _fontFamily = new FontFamily(string.IsNullOrWhiteSpace(f.ConsoleFamily) ? "Consolas" : f.ConsoleFamily);
        _fontSize = Math.Clamp(double.IsFinite(f.ConsoleSize) ? f.ConsoleSize : 13, 8, 32);
        // 고정폭 가정 셀 폭 실측('0' 20자 평균) — 폰트별 advance 차이(Consolas 0.55 가정 탈피).
        var probe = new TextBlock { FontFamily = _fontFamily, FontSize = _fontSize, Text = new string('0', 20) };
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        _charW = probe.DesiredSize.Width > 0 ? probe.DesiredSize.Width / 20 : _fontSize * 0.55;
        _lineH = Math.Max(_fontSize * 1.35, probe.DesiredSize.Height);
        if (_started)
        {
            ResizeToView();   // 격자(cols/rows) 재계산 → ConPTY 리사이즈 + 재렌더(MarkDirty 포함)
        }
    }

    /// <summary>세션 시작(lazy — 터미널 탭 활성 시에만). 이미 시작이면 포커스만.
    /// <b>레이아웃 전(ActualSize 0)이면 첫 실측 후로 지연</b> — 최소 격자(20×5)로 세션이 열리고 곧바로
    /// 리사이즈되며 초기 출력이 어긋나던 문제(상단 잘림·캐럿 행 불일치) 방지.</summary>
    public void Start()
    {
        if (_started)
        {
            FocusSoon();
            return;
        }
        if (ActualHeight < 1 || ActualWidth < 1)
        {
            SizeChanged += StartWhenSized;   // 실제 크기 확정 후 시작(중복 가드는 _started)
            return;
        }
        _started = true;
        StartSession(reset: true);
        FocusSoon();
    }

    private void StartWhenSized(object sender, SizeChangedEventArgs e)
    {
        if (ActualHeight < 1 || ActualWidth < 1)
        {
            return;
        }
        SizeChanged -= StartWhenSized;
        Start();
    }

    /// <summary>레이아웃/가시화 직후 포커스(즉시 Focus가 실패하는 타이밍 회피) — 키보드 입력 캡처.</summary>
    private void FocusSoon()
    {
        Focus(FocusState.Programmatic);
        _dispatcher.TryEnqueue(() => Focus(FocusState.Programmatic));
    }

    /// <summary>새 셸 세션 시작(<paramref name="reset"/>이면 화면 버퍼도 초기화 = 리셋). 작업 디렉터리는 이 시점의 선택 탭 폴더.</summary>
    private void StartSession(bool reset)
    {
        try
        {
            var (cols, rows) = MeasureGrid();
            if (reset || _vt is null)
            {
                _vt = new VtScreen(cols, rows);
            }
            string? cwd = WorkingDirectoryProvider?.Invoke();   // (재)시작 시점의 선택 탭 디렉터리
            var session = new ConPtySession();
            session.Output += OnSessionOutput;
            session.Exited += OnSessionExited;
            _session = session;
            session.Start(ConPtySession.DefaultShell(), string.IsNullOrEmpty(cwd) ? null : cwd, (short)cols, (short)rows);
            if (_pendingInput is string pending)   // 시작 전 요청된 입력(cd 등) — ConPTY가 버퍼링
            {
                _pendingInput = null;
                session.Write(pending);
            }
        }
        catch (Exception ex)
        {
            LinesPanel.Children.Add(new TextBlock { Text = Loc.T("term.startFail", ex.Message), Foreground = Brush(0xFFE74856), FontFamily = _fontFamily, FontSize = _fontSize });
        }
    }

    /// <summary>셸 종료 처리 — <c>exit</c> 등으로 종료되면 <b>리셋 후 재시작</b>. 패널/창 닫힘(Stop)이면 재시작 안 함.</summary>
    private void OnSessionExited() => _dispatcher.TryEnqueue(() =>
    {
        if (_stopping)
        {
            return;
        }
        var old = _session;
        _session = null;
        try { old?.Dispose(); } catch { }
        StartSession(reset: true);   // 새 셸(화면 초기화) — "리셋" 개념
        MarkDirty();
        Focus(FocusState.Programmatic);
    });

    public void Stop()
    {
        _stopping = true;
        _renderTimer.Stop();
        _blinkTimer.Stop();
        _session?.Dispose();
        _session = null;
    }

    private void OnSessionOutput(string text) => _dispatcher.TryEnqueue(() =>
    {
        _vt?.Feed(text);
        MarkDirty();
    });

    private void MarkDirty()
    {
        _dirty = true;
        if (!_renderTimer.IsRunning)
        {
            _renderTimer.Start();
        }
    }

    private void RenderScreen()
    {
        if (_vt is null)
        {
            return;
        }
        var lines = _vt.Lines;
        int start = Math.Max(0, lines.Count - RenderCap);
        LinesPanel.Children.Clear();
        var fontFamily = _fontFamily;   // 설정 콘솔 글꼴(ApplyFont)
        double maxLineWidth = 0;   // Canvas는 자식 크기를 반영하지 않음 → 스크롤 익스텐트용 명시 크기

        for (int li = start; li < lines.Count; li++)
        {
            TermCell[] cells = lines[li];
            // 뒤쪽 기본배경 공백은 잘라 렌더 비용 절감.
            int end = cells.Length;
            while (end > 0 && cells[end - 1].Ch is ' ' or '\0' && cells[end - 1].Bg == VtScreen.DefaultBg)
            {
                end--;
            }

            // 고정폭 셀 그리드: run을 Canvas에 열×_charW 절대 위치로 배치 — 폭이 다른 글리프(폴백 폰트·전각)로
            // 인한 열 드리프트가 다음 run으로 누적되지 않는다(캐럿·열 정렬의 전제).
            var line = new Canvas { Height = _lineH, Width = Math.Max(1, end) * _charW };
            Canvas.SetTop(line, (li - start) * _lineH);   // 절대 배치 — 오버레이(캐럿/선택)와 동일 수식
            maxLineWidth = Math.Max(maxLineWidth, (double)line.Width);
            int c = 0;
            while (c < end)
            {
                if (cells[c].Ch == '\0')
                {
                    c++;   // 전각(2칸) 연속 셀 — 앞 글자가 이미 두 칸을 차지
                    continue;
                }
                int runStart = c;
                var sb = new StringBuilder();
                int runCols;
                if (IsAsciiPrintable(cells[c].Ch))
                {
                    // ASCII run: Consolas 고정폭이라 run 내부 드리프트 없음 — 스타일 단위로 묶어 요소 수 절감.
                    while (c < end && IsAsciiPrintable(cells[c].Ch) && SameStyle(cells[c], cells[runStart]))
                    {
                        sb.Append(cells[c].Ch);
                        c++;
                    }
                    runCols = c - runStart;
                }
                else
                {
                    // 비ASCII(한글·글리프 등): 폴백 폰트 폭이 제각각 → 한 글자씩 자기 열에 고정 배치.
                    char ch = cells[c].Ch;
                    sb.Append(ch);
                    runCols = VtScreen.IsWide(ch) ? 2 : 1;
                    c++;
                }
                var s = cells[runStart];
                uint fg = s.Reverse ? s.Bg : s.Fg;
                uint bg = s.Reverse ? s.Fg : s.Bg;
                var tb = new TextBlock
                {
                    Text = sb.ToString(),
                    FontFamily = fontFamily,
                    FontSize = _fontSize,
                    Foreground = Brush(fg),
                    FontWeight = s.Bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                    LineHeight = _lineH,
                    IsTextSelectionEnabled = false,
                    // faint(SGR 2): PSReadLine 인라인 예측(history)을 연한 회색 미리보기로 — VS Code와 동일한 느낌.
                    Opacity = s.Faint ? 0.45 : 1.0,
                };
                FrameworkElement el = bg != VtScreen.DefaultBg
                    ? new Border { Background = Brush(bg), Width = runCols * _charW, Height = _lineH, Child = tb }
                    : tb;
                Canvas.SetLeft(el, runStart * _charW);
                line.Children.Add(el);
            }
            LinesPanel.Children.Add(line);
        }
        // Canvas는 자식으로 크기가 잡히지 않음 → 스크롤 익스텐트를 명시(세로=행수×_lineH, 가로=최장 줄).
        LinesPanel.Height = (lines.Count - start) * _lineH;
        LinesPanel.Width = maxLineWidth;

        // 맨 아래로 스크롤(항상 최신 보이게).
        Scroll.UpdateLayout();
        Scroll.ChangeView(null, Scroll.ScrollableHeight, null, disableAnimation: true);

        _caretOn = true;   // 출력 직후엔 캐럿을 보이게(활동 시 항상 표시)
        UpdateCaret();
        RenderSelection();   // 선택은 절대 라인 기준 — 새 출력으로 렌더 시작점이 밀려도 하이라이트 재배치
    }

    /// <summary>커서 위치에 캐럿(블록)을 오버레이. 포커스=반투명 채운 블록(깜빡임), 비포커스=외곽선(중공).</summary>
    private void UpdateCaret()
    {
        if (_vt is null || _session is null)
        {
            Caret.Visibility = Visibility.Collapsed;
            return;
        }
        int count = _vt.ScrollbackCount + _vt.Rows;
        int start = Math.Max(0, count - RenderCap);
        int cursorAbs = _vt.ScrollbackCount + _vt.CursorRow;
        int rendered = cursorAbs - start;                       // 렌더된 라인 목록에서의 행 인덱스
        if (rendered < 0 || rendered >= count - start)
        {
            Caret.Visibility = Visibility.Collapsed;
            return;
        }

        Caret.Width = _charW;
        Caret.Height = _lineH;
        Canvas.SetLeft(Caret, _vt.CursorCol * _charW);
        Canvas.SetTop(Caret, rendered * _lineH);

        if (_focused)
        {
            Caret.Background = _caretFill;
            Caret.BorderThickness = new Thickness(0);
            Caret.Visibility = _caretOn ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            Caret.Background = null;
            Caret.BorderBrush = _caretFill;
            Caret.BorderThickness = new Thickness(1);
            Caret.Visibility = Visibility.Visible;             // 비포커스 캐럿은 깜빡이지 않음
        }
    }

    private static bool SameStyle(TermCell a, TermCell b) =>
        a.Fg == b.Fg && a.Bg == b.Bg && a.Bold == b.Bold && a.Reverse == b.Reverse && a.Faint == b.Faint;

    /// <summary>Consolas가 직접 그리는 ASCII 인쇄 문자(고정폭 보장 — run으로 묶어도 드리프트 없음).</summary>
    private static bool IsAsciiPrintable(char ch) => ch >= ' ' && ch < '\x7F';

    private SolidColorBrush Brush(uint argb)
    {
        if (!_brushes.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Windows.UI.Color.FromArgb(
                (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
            _brushes[argb] = b;
        }
        return b;
    }

    // ── 마우스 선택(복사) — 절대 라인(스크롤백 포함)·셀 경계 기준. Ctrl+C(선택 시)/Ctrl+Shift+C=복사,
    //    Ctrl+V=붙여넣기. 선택 없으면 Ctrl+C는 기존대로 SIGINT. ─────────────────────────────
    private bool _selecting;
    private bool _hasSelection;
    private (int Line, int Col) _selAnchor;   // Col은 셀 경계(0..Cols) — [작은쪽, 큰쪽) 범위 선택
    private (int Line, int Col) _selEnd;

    /// <summary>뷰포트 좌표 → (절대 라인, 셀 경계 열). 렌더 범위(RenderCap)와 동일한 인덱스 기준.</summary>
    private (int Line, int Col) HitCell(Windows.Foundation.Point viewportPos)
    {
        if (_vt is null)
        {
            return (0, 0);
        }
        int count = _vt.ScrollbackCount + _vt.Rows;
        int start = Math.Max(0, count - RenderCap);
        int line = Math.Clamp(start + (int)Math.Floor((viewportPos.Y + Scroll.VerticalOffset) / _lineH), 0, count - 1);
        int col = Math.Clamp((int)Math.Round((viewportPos.X + Scroll.HorizontalOffset - 6) / _charW), 0, _vt.Cols);
        return (line, col);
    }

    // 동기 Focus는 pointer-pressed 처리 직후 WinUI 기본 포커스 로직에 덮여 "포커스 됐다 취소" 됨.
    // FocusSoon()은 dispatcher enqueue로 그 이후에 다시 포커스 → 클릭 포커스가 안정적으로 유지된다.
    private void OnTermPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FocusSoon();
        var pt = e.GetCurrentPoint(Scroll);
        // 우클릭: 드래그 선택이 있으면 그 영역을 복사(Windows Terminal 관례 — 복사 후 선택 해제).
        if (pt.Properties.IsRightButtonPressed)
        {
            if (_hasSelection)
            {
                CopySelection();
                e.Handled = true;
            }
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed)
        {
            return;
        }
        ClearSelection();
        _selecting = true;
        _selAnchor = _selEnd = HitCell(pt.Position);
        CapturePointer(e.Pointer);
    }

    private void OnTermPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }
        var pos = e.GetCurrentPoint(Scroll).Position;
        _selEnd = HitCell(pos);
        _hasSelection = _selEnd != _selAnchor;
        RenderSelection();
        UpdateSelAutoScroll(pos);   // 뷰포트 가장자리 대기 → 자동 스크롤(오프스크린 선택)
    }

    private void OnTermPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        FocusSoon();
        if (_selecting)
        {
            _selecting = false;
            _selScrollTimer?.Stop();
            ReleasePointerCaptures();
            if (!_hasSelection)
            {
                ClearSelection();
            }
        }
    }

    // ── 선택 드래그 자동 스크롤 — 뷰포트 밖(위/아래) 내용도 선택 가능하게(파일 목록 B-11과 동일 UX) ──
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _selScrollTimer;
    private double _selScrollDelta;                    // 틱당 스크롤(px, 부호=방향)
    private Windows.Foundation.Point _selLastPos;      // Scroll 기준 마지막 포인터 위치(틱마다 재판정)

    /// <summary>선택 드래그 중 포인터가 뷰포트 상/하단 가장자리에 있으면 자동 스크롤 타이머 가동 —
    /// 틱마다 스크롤을 밀고 같은 포인터 위치로 선택 끝을 재계산해 하이라이트를 늘린다.</summary>
    private void UpdateSelAutoScroll(Windows.Foundation.Point pos)
    {
        const double edge = 24;    // 가장자리 감지 폭(px)
        const double speed = 40;   // 틱당 스크롤(px)
        _selLastPos = pos;
        double h = Scroll.ActualHeight;
        _selScrollDelta = pos.Y < edge ? -speed : (pos.Y > h - edge ? speed : 0);
        if (_selScrollDelta == 0)
        {
            _selScrollTimer?.Stop();
            return;
        }
        if (_selScrollTimer is null)
        {
            _selScrollTimer = _dispatcher.CreateTimer();
            _selScrollTimer.Interval = TimeSpan.FromMilliseconds(60);
            _selScrollTimer.IsRepeating = true;
            _selScrollTimer.Tick += (_, _) =>
            {
                if (!_selecting)
                {
                    _selScrollTimer!.Stop();
                    return;
                }
                Scroll.ChangeView(null, Scroll.VerticalOffset + _selScrollDelta, null, disableAnimation: true);
                _selEnd = HitCell(_selLastPos);   // 새 오프셋 기준 같은 화면 위치 = 더 위/아래 라인
                _hasSelection = _selEnd != _selAnchor;
                RenderSelection();
            };
        }
        if (!_selScrollTimer.IsRunning)
        {
            _selScrollTimer.Start();
        }
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        SelLayer.Children.Clear();
    }

    private ((int Line, int Col) S, (int Line, int Col) E) OrderedSelection()
    {
        var a = _selAnchor;
        var b = _selEnd;
        bool aFirst = a.Line < b.Line || (a.Line == b.Line && a.Col <= b.Col);
        return aFirst ? (a, b) : (b, a);
    }

    /// <summary>선택 하이라이트 렌더 — 라인별 사각형(첫 줄=시작 열부터, 마지막 줄=끝 경계까지, 중간=전폭).</summary>
    private void RenderSelection()
    {
        SelLayer.Children.Clear();
        if (!_hasSelection || _vt is null)
        {
            return;
        }
        var (s, en) = OrderedSelection();
        int count = _vt.ScrollbackCount + _vt.Rows;
        int start = Math.Max(0, count - RenderCap);
        for (int li = Math.Max(s.Line, start); li <= Math.Min(en.Line, count - 1); li++)
        {
            int c0 = li == s.Line ? s.Col : 0;
            int c1 = li == en.Line ? en.Col : _vt.Cols;
            if (c1 <= c0)
            {
                continue;   // 끝 줄 경계가 시작 앞(빈 구간)
            }
            var r = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = (c1 - c0) * _charW,
                Height = _lineH,
                Fill = Brush(0x553D8BFF),
            };
            Canvas.SetLeft(r, c0 * _charW);
            Canvas.SetTop(r, (li - start) * _lineH);
            SelLayer.Children.Add(r);
        }
    }

    /// <summary>선택 텍스트를 클립보드로 복사 후 선택 해제.</summary>
    private void CopySelection()
    {
        if (!_hasSelection || _vt is null)
        {
            return;
        }
        var (s, en) = OrderedSelection();
        string text = _vt.GetText(s.Line, s.Col, en.Line, en.Col - 1);
        if (text.Length > 0)
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        ClearSelection();
    }

    /// <summary>클립보드 텍스트를 셸 입력으로(붙여넣기) — 터미널 개행은 CR.</summary>
    private async System.Threading.Tasks.Task PasteClipboardAsync()
    {
        try
        {
            var view = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (!view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                return;
            }
            string text = await view.GetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                _session?.Write(text.Replace("\r\n", "\r").Replace('\n', '\r'));
            }
        }
        catch
        {
            // 클립보드 접근 실패는 무해(무동작)
        }
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        _focused = true;
        _caretOn = true;
        _blinkTimer.Start();       // 포커스 시에만 깜빡임
        UpdateCaret();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        _focused = false;
        _blinkTimer.Stop();
        UpdateCaret();             // 중공(외곽선) 캐럿으로 전환
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }
        char c = e.Character;
        // Tab('\t')·Backspace('\b')는 OnKeyDown에서 처리 — 여기선 제외해 중복 입력 방지.
        if (c == '\r' || c >= ' ')
        {
            _session.Write(c.ToString());
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }
        string? seq = e.Key switch
        {
            // Tab은 셸 자동완성용으로 셸에 보낸다(Handled=true로 WinUI 포커스 이동 차단). Shift+Tab=역방향 완성.
            VirtualKey.Tab => IsShiftDown() ? "\x1B[Z" : "\t",
            // Backspace=DEL(0x7F, 1글자 삭제). 0x08(^H)은 ConPTY에서 Ctrl+Backspace(단어 삭제)로 해석되므로
            // Ctrl 눌렀을 때만 0x08 — 실제 터미널과 동일한 매핑.
            VirtualKey.Back => IsCtrlDown() ? "\b" : "\x7F",
            VirtualKey.Up => "\x1B[A",
            VirtualKey.Down => "\x1B[B",
            VirtualKey.Right => "\x1B[C",
            VirtualKey.Left => "\x1B[D",
            VirtualKey.Home => "\x1B[H",
            VirtualKey.End => "\x1B[F",
            VirtualKey.Delete => "\x1B[3~",
            VirtualKey.PageUp => "\x1B[5~",
            VirtualKey.PageDown => "\x1B[6~",
            VirtualKey.Escape => "\x1B",
            _ => null,
        };
        if (seq is not null)
        {
            _session.Write(seq);
            e.Handled = true;
            return;
        }
        // 복사: 선택이 있으면 Ctrl+C=복사(SIGINT 아님 — Windows Terminal 관례), Ctrl+Shift+C=항상 복사.
        if (IsCtrlDown() && e.Key == VirtualKey.C && (_hasSelection || IsShiftDown()))
        {
            e.Handled = true;
            CopySelection();
            return;
        }
        // 붙여넣기: Ctrl+V(/Ctrl+Shift+V) — 클립보드 텍스트를 셸 입력으로.
        if (IsCtrlDown() && e.Key == VirtualKey.V)
        {
            e.Handled = true;
            _ = PasteClipboardAsync();
            return;
        }
        if (IsCtrlDown() && e.Key >= VirtualKey.A && e.Key <= VirtualKey.Z)
        {
            _session.Write(((char)(e.Key - VirtualKey.A + 1)).ToString());   // Ctrl+A..Z = 0x01..0x1A
            e.Handled = true;
        }
    }

    private static bool IsCtrlDown()
        => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private static bool IsShiftDown()
        => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private void ResizeToView()
    {
        var (cols, rows) = MeasureGrid();
        _vt?.Resize(cols, rows);
        _session?.Resize((short)cols, (short)rows);
        MarkDirty();
    }

    private (int cols, int rows) MeasureGrid()
    {
        int cols = Math.Clamp((int)((ActualWidth - 12) / _charW), 20, 500);   // 좌우 패딩(6×2) 제외
        int rows = Math.Clamp((int)(ActualHeight / _lineH), 5, 200);
        return (cols, rows);
    }
}
