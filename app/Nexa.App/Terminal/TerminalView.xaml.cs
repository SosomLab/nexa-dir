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

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _renderTimer;
    private readonly Dictionary<uint, SolidColorBrush> _brushes = new();
    private ConPtySession? _session;
    private VtScreen? _vt;
    private bool _started;
    private bool _dirty;
    private bool _stopping;         // Stop()로 인한 종료(재시작 금지) vs exit로 인한 종료(재시작) 구분

    /// <summary>셸 (재)시작 시 작업 디렉터리를 반환(호스트가 선택 탭 폴더 제공). (재)시작 시점 값만 사용 — 이후 폴더 변경/탭 이동은 무영향.</summary>
    public Func<string?>? WorkingDirectoryProvider { get; set; }

    public TerminalView()
    {
        InitializeComponent();
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _renderTimer = _dispatcher.CreateTimer();
        _renderTimer.Interval = TimeSpan.FromMilliseconds(33);   // ~30fps 코얼레싱
        _renderTimer.IsRepeating = false;
        _renderTimer.Tick += (_, _) => { if (_dirty) { _dirty = false; RenderScreen(); } };
        SizeChanged += (_, _) => ResizeToView();
        Unloaded += (_, _) => Stop();
    }

    /// <summary>세션 시작(lazy — 터미널 탭 활성 시에만). 이미 시작이면 포커스만.</summary>
    public void Start()
    {
        if (_started)
        {
            FocusSoon();
            return;
        }
        _started = true;
        StartSession(reset: true);
        FocusSoon();
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

            var line = new StackPanel { Orientation = Orientation.Horizontal, Height = LineH };
            int c = 0;
            while (c < end)
            {
                int runStart = c;
                var sb = new StringBuilder();
                while (c < end && SameStyle(cells[c], cells[runStart]))
                {
                    char ch = cells[c].Ch;
                    sb.Append(ch == '\0' ? ' ' : ch);
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
                };
                if (bg != VtScreen.DefaultBg)
                {
                    line.Children.Add(new Border { Background = Brush(bg), Child = tb });
                }
                else
                {
                    line.Children.Add(tb);
                }
            }
            if (line.Children.Count == 0)
            {
                line.Children.Add(new TextBlock { Text = " ", FontSize = FontSizePx, Height = LineH });   // 빈 줄 높이 유지
            }
            LinesPanel.Children.Add(line);
        }

        // 맨 아래로 스크롤(항상 최신 보이게).
        Scroll.UpdateLayout();
        Scroll.ChangeView(null, Scroll.ScrollableHeight, null, disableAnimation: true);
    }

    private static bool SameStyle(TermCell a, TermCell b) =>
        a.Fg == b.Fg && a.Bg == b.Bg && a.Bold == b.Bold && a.Reverse == b.Reverse;

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

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e) => Focus(FocusState.Programmatic);

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }
        char c = e.Character;
        if (c == '\r' || c == '\t' || c == '\b' || c >= ' ')
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
        if (IsCtrlDown() && e.Key >= VirtualKey.A && e.Key <= VirtualKey.Z)
        {
            _session.Write(((char)(e.Key - VirtualKey.A + 1)).ToString());   // Ctrl+A..Z = 0x01..0x1A
            e.Handled = true;
        }
    }

    private static bool IsCtrlDown()
        => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private void ResizeToView()
    {
        var (cols, rows) = MeasureGrid();
        _vt?.Resize(cols, rows);
        _session?.Resize((short)cols, (short)rows);
        MarkDirty();
    }

    private (int cols, int rows) MeasureGrid()
    {
        int cols = Math.Clamp((int)(ActualWidth / CharW), 20, 500);
        int rows = Math.Clamp((int)(ActualHeight / LineH), 5, 200);
        return (cols, rows);
    }
}
