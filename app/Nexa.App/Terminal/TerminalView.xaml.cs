using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Nexa.App.Terminal;

/// <summary>
/// 터미널 뷰(BP-T1 기본) — <see cref="ConPtySession"/>이 구동한 셸의 출력을 표시하고 키 입력을 셸로 보낸다.
/// <b>이번 슬라이스는 토대</b>: 출력의 VT 시퀀스는 <b>일단 제거해 평문으로 append</b>(읽기용). 커서/색/화면 버퍼가 있는
/// 실제 VT 에뮬레이터(스크린 그리드 렌더)는 다음 슬라이스. 세션은 <see cref="Start"/> 호출 시에만 시작(lazy).
/// </summary>
public sealed partial class TerminalView : UserControl
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private ConPtySession? _session;
    private bool _started;
    private readonly StringBuilder _buffer = new();

    // CSI/OSC/기타 이스케이프 시퀀스 제거(슬라이스 1: 평문 표시). 다음 슬라이스에서 파서로 대체.
    private static readonly Regex Ansi = new(
        "\x1B\\[[0-9;?]*[ -/]*[@-~]|\x1B\\][^\x07\x1B]*(\x07|\x1B\\\\)|\x1B[@-Z\\\\-_]",
        RegexOptions.Compiled);

    public TerminalView()
    {
        InitializeComponent();
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        SizeChanged += (_, _) => ResizeToView();
        Unloaded += (_, _) => Stop();
    }

    /// <summary>세션 시작(lazy — 터미널 탭이 실제로 활성화될 때만 호출). 이미 시작됐으면 무시.</summary>
    public void Start(string? workingDir)
    {
        if (_started)
        {
            Focus(FocusState.Programmatic);
            return;
        }
        _started = true;
        try
        {
            _session = new ConPtySession();
            _session.Output += OnSessionOutput;
            _session.Exited += () => _dispatcher.TryEnqueue(() => AppendPlain("\n[프로세스 종료됨]\n"));
            var (cols, rows) = MeasureGrid();
            _session.Start(ConPtySession.DefaultShell(), workingDir, cols, rows);
        }
        catch (Exception ex)
        {
            AppendPlain($"[터미널 시작 실패] {ex.Message}\n");
        }
        Focus(FocusState.Programmatic);
    }

    /// <summary>세션 종료(패널/창 닫힘·언로드).</summary>
    public void Stop()
    {
        _session?.Dispose();
        _session = null;
    }

    private void OnSessionOutput(string text) => _dispatcher.TryEnqueue(() => AppendPlain(text));

    private void AppendPlain(string text)
    {
        // 이스케이프 제거 + 개행 정규화(슬라이스 1). \r 단독은 버림, \b는 한 글자 지움(근사).
        string clean = Ansi.Replace(text, string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (char c in clean)
        {
            if (c == '\b')
            {
                if (_buffer.Length > 0 && _buffer[^1] != '\n')
                {
                    _buffer.Length--;
                }
            }
            else if (c >= ' ' || c == '\n' || c == '\t')
            {
                _buffer.Append(c);
            }
        }
        // 버퍼 상한(메모리 규율) — 뒤쪽 200KB만 유지.
        const int cap = 200_000;
        if (_buffer.Length > cap)
        {
            _buffer.Remove(0, _buffer.Length - cap);
        }
        OutputText.Text = _buffer.ToString();
        Scroll.ChangeView(null, Scroll.ScrollableHeight, null, disableAnimation: true);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e) => Focus(FocusState.Programmatic);

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }
        char c = e.Character;
        // 제어문자 일부는 KeyDown에서 처리(중복 방지). 여기선 출력 가능 문자 + Enter/Tab/Backspace.
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
        // 방향키/특수키는 VT 시퀀스로. (문자·Enter는 CharacterReceived가 처리.)
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
        // Ctrl+C/D/Z 등 제어.
        if (IsCtrlDown())
        {
            if (e.Key >= VirtualKey.A && e.Key <= VirtualKey.Z)
            {
                _session.Write(((char)(e.Key - VirtualKey.A + 1)).ToString());   // Ctrl+A..Z = 0x01..0x1A
                e.Handled = true;
            }
        }
    }

    private static bool IsCtrlDown()
        => (InputKeyboardSourceState() & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private static CoreVirtualKeyStates InputKeyboardSourceState()
        => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

    private void ResizeToView()
    {
        var (cols, rows) = MeasureGrid();
        _session?.Resize(cols, rows);
    }

    /// <summary>뷰 크기에서 열/행 근사(Consolas 12 기준). 슬라이스 1 근사치.</summary>
    private (short cols, short rows) MeasureGrid()
    {
        const double charW = 7.0;   // Consolas 12 대략 폭
        const double lineH = 16.0;  // 대략 행 높이
        short cols = (short)Math.Clamp((int)(ActualWidth / charW), 20, 500);
        short rows = (short)Math.Clamp((int)(ActualHeight / lineH), 5, 200);
        return (cols, rows);
    }
}
