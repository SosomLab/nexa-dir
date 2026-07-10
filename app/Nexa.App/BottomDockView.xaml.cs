using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Nexa.App.Terminal;
using Nexa.Plugins.Preview;

namespace Nexa.App;

/// <summary>하단 도킹 패널이 호스팅할 콘텐츠 종류(BP-1). Hex/터미널은 후속 구현.</summary>
public enum BottomPanelKind
{
    Info,
    Preview,
    Hex,
    Terminal,
}

/// <summary>
/// 하단 도킹 패널의 <b>콘텐츠 호스트</b>(재사용 — 좌/우 도킹에 각각 배치). 종류 선택(정보/미리보기/Hex/터미널)에
/// 따라 콘텐츠를 스왑한다. 정보=<see cref="InfoText"/>, **미리보기=<see cref="PreviewPath"/>의 파일을
/// 공급자(<see cref="PreviewRegistry"/>)로 렌더**(텍스트/이미지 등, BP-2). Hex/터미널은 후속.
/// </summary>
public sealed partial class BottomDockView : UserControl
{
    private readonly DispatcherQueueTimer _previewTimer;   // 미리보기 로딩 부하 방지(디바운스, 아이콘 로딩식 wrapper)

    public BottomDockView()
    {
        InitializeComponent();
        // 미리보기 영역이 리사이즈되면 공급자에 새 크기로 다시 렌더(크기 상호연동, BP-2).
        PreviewHost.SizeChanged += OnPreviewHostSizeChanged;
        // 미리보기 디바운스: 선택이 빠르게 바뀌어도 마지막 것만 렌더(부하 방지). 호스트가 처리 → 공급자는 몰라도 됨.
        _previewTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _previewTimer.Interval = TimeSpan.FromMilliseconds(150);
        _previewTimer.IsRepeating = false;
        _previewTimer.Tick += (_, _) => { _ = RenderPreviewAsync(); };
        Unloaded += (_, _) => _terminalView?.Stop();   // 창/패널 닫힘 → 터미널 세션 종료
        Render();
    }

    /// <summary>현재 폴더(터미널 작업 디렉터리 등). 호스트(MainWindow)가 패널 경로로 설정.</summary>
    public static readonly DependencyProperty CurrentFolderProperty = DependencyProperty.Register(
        nameof(CurrentFolder), typeof(string), typeof(BottomDockView), new PropertyMetadata(string.Empty));

    public string CurrentFolder
    {
        get => (string)GetValue(CurrentFolderProperty);
        set => SetValue(CurrentFolderProperty, value);
    }

    /// <summary>정보 콘텐츠 텍스트(예: 현재 폴더/선택 항목). 호스트(MainWindow)가 설정.</summary>
    public static readonly DependencyProperty InfoTextProperty = DependencyProperty.Register(
        nameof(InfoText), typeof(string), typeof(BottomDockView),
        new PropertyMetadata(string.Empty, (d, _) => ((BottomDockView)d).Render()));

    public string InfoText
    {
        get => (string)GetValue(InfoTextProperty);
        set => SetValue(InfoTextProperty, value);
    }

    /// <summary>미리보기 대상 파일 경로(빈 문자열=대상 없음). 호스트가 선택 항목에 맞춰 설정.</summary>
    public static readonly DependencyProperty PreviewPathProperty = DependencyProperty.Register(
        nameof(PreviewPath), typeof(string), typeof(BottomDockView),
        new PropertyMetadata(string.Empty, (d, _) => ((BottomDockView)d).OnPreviewPathChanged()));

    public string PreviewPath
    {
        get => (string)GetValue(PreviewPathProperty);
        set => SetValue(PreviewPathProperty, value);
    }

    private BottomPanelKind _kind = BottomPanelKind.Info;

    /// <summary>선택된 콘텐츠 종류.</summary>
    public BottomPanelKind Kind
    {
        get => _kind;
        set
        {
            if (_kind != value)
            {
                _kind = value;
                SyncToggles();
                Render();
                KindChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>콘텐츠 종류가 바뀌었을 때(세션 저장 등 호스트 반응용).</summary>
    public event EventHandler? KindChanged;

    /// <summary>설정 글꼴 반영(PREF-3) — 기본 글꼴(정보 텍스트·종류 버튼) + 콘솔 글꼴(터미널 전달).</summary>
    public void ApplyFonts()
    {
        var f = AppSettings.Fonts;
        var fam = new Microsoft.UI.Xaml.Media.FontFamily(f.BaseFamily);
        ContentText.FontFamily = fam;
        ContentText.FontSize = f.BaseSize;
        foreach (Control kind in new Control[] { KindInfo, KindPreview, KindHex, KindTerminal })
        {
            kind.FontFamily = fam;
        }
        _terminalView?.ApplyFont();
    }

    private void OnKindClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag && Enum.TryParse<BottomPanelKind>(tag, out var k))
        {
            Kind = k;
        }
        SyncToggles();   // 라디오식: 항상 선택 하나 유지
    }

    private void SyncToggles()
    {
        KindInfo.IsChecked = _kind == BottomPanelKind.Info;
        KindPreview.IsChecked = _kind == BottomPanelKind.Preview;
        KindHex.IsChecked = _kind == BottomPanelKind.Hex;
        KindTerminal.IsChecked = _kind == BottomPanelKind.Terminal;
    }

    private void Render()
    {
        bool preview = _kind == BottomPanelKind.Preview;
        bool terminal = _kind == BottomPanelKind.Terminal;
        PreviewHost.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        TerminalHost.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;
        TextScroll.Visibility = (preview || terminal) ? Visibility.Collapsed : Visibility.Visible;

        if (preview)
        {
            SchedulePreview();   // 디바운스 렌더(부하 방지)
            return;
        }
        if (terminal)
        {
            EnsureTerminal();    // lazy — 이 시점(터미널 탭 활성)에만 세션 생성/시작
            return;
        }

        ContentText.Text = _kind switch
        {
            BottomPanelKind.Info => string.IsNullOrEmpty(InfoText) ? Loc.T("dock.noInfo") : InfoText,
            BottomPanelKind.Hex => Loc.T("dock.hexPending"),
            _ => string.Empty,
        };
    }

    /// <summary>정보란 터미널 토글 옆 "터미널 위치 이동" 버튼 — 이 도크 패널의 현재 탭 폴더로 cd.</summary>
    private void OnTerminalCd(object sender, RoutedEventArgs e)
    {
        string folder = CurrentFolder;
        if (!string.IsNullOrEmpty(folder))
        {
            TerminalCdTo(folder);
        }
    }

    /// <summary>터미널 탭으로 전환 후 셸 작업 디렉터리를 <paramref name="folder"/>로 이동(cd) —
    /// 도구 모음 "터미널 위치 이동". 미생성이면 생성·시작 후 전송(TerminalView pending).</summary>
    public void TerminalCdTo(string folder)
    {
        Kind = BottomPanelKind.Terminal;   // Render → EnsureTerminal(생성·시작)
        EnsureTerminal();                  // Kind가 이미 Terminal이어도 보장(멱등)
        _terminalView?.ChangeDirectory(folder);
    }

    // ── 터미널 lazy 로딩(BP-T) ────────────────────────────────────────
    private TerminalView? _terminalView;

    /// <summary>터미널 탭이 실제 활성화될 때만 TerminalView 생성 + 세션 시작(lazy). 이후 유지(탭 전환에도 세션 생존).</summary>
    private void EnsureTerminal()
    {
        if (_terminalView is null)
        {
            _terminalView = new TerminalView();
            // (재)시작 시점의 활성 탭 폴더를 작업 디렉터리로. 없거나 유효하지 않으면 사용자 홈으로 폴백.
            _terminalView.WorkingDirectoryProvider = () =>
            {
                string cur = CurrentFolder;
                try
                {
                    if (!string.IsNullOrEmpty(cur) && Directory.Exists(cur))
                    {
                        return cur;
                    }
                }
                catch
                {
                    // 접근 예외 등 → 홈 폴백
                }
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            };
            TerminalHost.Child = _terminalView;
        }
        _terminalView.Start();   // 멱등(이미 시작이면 포커스만)
    }

    // ── 미리보기 렌더(BP-2) — 로딩 부하 방지 wrapper(디바운스·취소·중복 스킵). 공급자는 몰라도 됨 ─────
    private CancellationTokenSource? _previewCts;
    private double _lastRenderW, _lastRenderH;   // 마지막 렌더 시 영역 크기(리사이즈 재렌더 임계 비교)
    private string _lastRenderedPath = string.Empty;

    /// <summary>디바운스 예약 — 빠른 선택 전환 시 마지막 것만 렌더(부하 방지).</summary>
    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnPreviewPathChanged()
    {
        if (_kind == BottomPanelKind.Preview)
        {
            SchedulePreview();
        }
    }

    /// <summary>미리보기 영역 크기 변경 → 공급자에 새 크기로 재렌더(임계 초과 시). 크기 상호연동.</summary>
    private void OnPreviewHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_kind != BottomPanelKind.Preview || string.IsNullOrEmpty(PreviewPath))
        {
            return;
        }
        // 작은 변동엔 재렌더하지 않음(리사이즈 드래그 중 과도 방지).
        if (Math.Abs(e.NewSize.Width - _lastRenderW) < 40 && Math.Abs(e.NewSize.Height - _lastRenderH) < 40)
        {
            return;
        }
        SchedulePreview();   // 디바운스로 재렌더
    }

    private async Task RenderPreviewAsync()
    {
        string path = PreviewPath;
        if (string.IsNullOrEmpty(path))
        {
            _previewCts?.Cancel();
            _lastRenderedPath = string.Empty;
            SetPreviewMessage(Loc.T("preview.noItem"));
            return;
        }

        // 중복 스킵(부하 방지): 같은 파일 + 영역 크기 변화 없으면 이미 표시 중 → 재렌더 안 함.
        if (path == _lastRenderedPath &&
            Math.Abs(PreviewHost.ActualWidth - _lastRenderW) < 40 &&
            Math.Abs(PreviewHost.ActualHeight - _lastRenderH) < 40)
        {
            return;
        }

        // 이전 렌더 취소(빠른 선택 전환).
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;
        var ct = cts.Token;

        var provider = PreviewRegistry.Find(path);
        if (provider is null)
        {
            _lastRenderedPath = path;
            SetPreviewMessage(Loc.T("preview.unsupported", System.IO.Path.GetFileName(path)));
            return;
        }

        SetPreviewMessage(Loc.T("preview.loading"));
        _lastRenderW = PreviewHost.ActualWidth;
        _lastRenderH = PreviewHost.ActualHeight;
        try
        {
            var request = new PreviewRequest(path, _lastRenderW, _lastRenderH);
            var element = await provider.CreatePreviewAsync(request, ct);
            if (ct.IsCancellationRequested)
            {
                return;   // 다른 선택으로 대체됨
            }
            _lastRenderedPath = path;
            PreviewHost.Child = element ?? MessageBlock(Loc.T("preview.cantCreate"));
        }
        catch (OperationCanceledException)
        {
            // 취소는 무시
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                SetPreviewMessage(Loc.T("preview.fail", ex.Message));
            }
        }
    }

    private void SetPreviewMessage(string text) => PreviewHost.Child = MessageBlock(text);

    private static TextBlock MessageBlock(string text) => new()
    {
        Text = text,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(AppSettings.Fonts.BaseFamily),
        FontSize = AppSettings.Fonts.BaseSize,
        Opacity = 0.6,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
    };
}
