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
/// <para>글리프: 파워라인/Nerd Font 아이콘은 고정폭 폰트(Consolas)에 없어 네모로 보일 수 있다(색은 정상) — 폰트 설정 후속.</para>
/// </summary>
public sealed partial class TerminalView : UserControl
{
    private const double FontSizePx = 13;
    private const double CharW = FontSizePx * 0.55;   // Consolas 대략 advance
    private const double LineH = FontSizePx * 1.35;
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
        }
        catch (Exception ex)
        {
            LinesPanel.Children.Add(new TextBlock { Text = $"[터미널 시작 실패] {ex.Message}", Foreground = Brush(0xFFE74856), FontFamily = new FontFamily("Consolas"), FontSize = FontSizePx });
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
        var fontFamily = new FontFamily("Consolas");

        for (int li = start; li < lines.Count; li++)
        {
            TermCell[] cells = lines[li];
            // 뒤쪽 기본배경 공백은 잘라 렌더 비용 절감.
            int end = cells.Length;
            while (end > 0 && cells[end - 1].Ch is ' ' or '\0' && cells[end - 1].Bg == VtScreen.DefaultBg)
            {
                end--;
            }

            // 고정폭 셀 그리드: run을 Canvas에 열×CharW 절대 위치로 배치 — 폭이 다른 글리프(폴백 폰트·전각)로
            // 인한 열 드리프트가 다음 run으로 누적되지 않는다(캐럿·열 정렬의 전제).
            // 명시 Width + 기본 Stretch 정렬은 가운데 배치로 동작 → 반드시 Left 고정.
            var line = new Canvas { Height = LineH, Width = Math.Max(1, end) * CharW, HorizontalAlignment = HorizontalAlignment.Left };
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
                    FontSize = FontSizePx,
                    Foreground = Brush(fg),
                    FontWeight = s.Bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                    LineHeight = LineH,
                    IsTextSelectionEnabled = false,
                    // faint(SGR 2): PSReadLine 인라인 예측(history)을 연한 회색 미리보기로 — VS Code와 동일한 느낌.
                    Opacity = s.Faint ? 0.45 : 1.0,
                };
                FrameworkElement el = bg != VtScreen.DefaultBg
                    ? new Border { Background = Brush(bg), Width = runCols * CharW, Height = LineH, Child = tb }
                    : tb;
                Canvas.SetLeft(el, runStart * CharW);
                line.Children.Add(el);
            }
            LinesPanel.Children.Add(line);
        }

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

        Caret.Width = CharW;
        Caret.Height = LineH;
        Canvas.SetLeft(Caret, _vt.CursorCol * CharW);
        Canvas.SetTop(Caret, rendered * LineH);

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
        int line = Math.Clamp(start + (int)Math.Floor((viewportPos.Y + Scroll.VerticalOffset) / LineH), 0, count - 1);
        int col = Math.Clamp((int)Math.Round((viewportPos.X + Scroll.HorizontalOffset - 6) / CharW), 0, _vt.Cols);
        return (line, col);
    }

    // 동기 Focus는 pointer-pressed 처리 직후 WinUI 기본 포커스 로직에 덮여 "포커스 됐다 취소" 됨.
    // FocusSoon()은 dispatcher enqueue로 그 이후에 다시 포커스 → 클릭 포커스가 안정적으로 유지된다.
    private void OnTermPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FocusSoon();
        var pt = e.GetCurrentPoint(Scroll);
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
        _selEnd = HitCell(e.GetCurrentPoint(Scroll).Position);
        _hasSelection = _selEnd != _selAnchor;
        RenderSelection();
    }

    private void OnTermPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        FocusSoon();
        if (_selecting)
        {
            _selecting = false;
            ReleasePointerCaptures();
            if (!_hasSelection)
            {
                ClearSelection();
            }
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
                Width = (c1 - c0) * CharW,
                Height = LineH,
                Fill = Brush(0x553D8BFF),
            };
            Canvas.SetLeft(r, c0 * CharW);
            Canvas.SetTop(r, (li - start) * LineH);
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
        int cols = Math.Clamp((int)((ActualWidth - 12) / CharW), 20, 500);   // 좌우 패딩(6×2) 제외
        int rows = Math.Clamp((int)(ActualHeight / LineH), 5, 200);
        return (cols, rows);
    }
}
