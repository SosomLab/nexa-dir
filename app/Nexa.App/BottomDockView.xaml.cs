using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Nexa.App.Preview;

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
    public BottomDockView()
    {
        InitializeComponent();
        Render();
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
        PreviewHost.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        TextScroll.Visibility = preview ? Visibility.Collapsed : Visibility.Visible;

        if (preview)
        {
            _ = RenderPreviewAsync();
            return;
        }

        ContentText.Text = _kind switch
        {
            BottomPanelKind.Info => string.IsNullOrEmpty(InfoText) ? "(정보 없음)" : InfoText,
            BottomPanelKind.Hex => "Hex 뷰 — 준비 중 (후속 구현)",
            BottomPanelKind.Terminal => "터미널(ConPTY) — 준비 중 (후속 구현)",
            _ => string.Empty,
        };
    }

    // ── 미리보기 렌더(BP-2) ───────────────────────────────────────────
    private CancellationTokenSource? _previewCts;

    private void OnPreviewPathChanged()
    {
        if (_kind == BottomPanelKind.Preview)
        {
            _ = RenderPreviewAsync();
        }
    }

    private async Task RenderPreviewAsync()
    {
        // 빠른 선택 전환 시 이전 렌더 취소.
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;
        var ct = cts.Token;

        string path = PreviewPath;
        if (string.IsNullOrEmpty(path))
        {
            SetPreviewMessage("미리볼 항목이 없습니다 (파일을 선택하세요).");
            return;
        }

        var provider = PreviewRegistry.Find(path);
        if (provider is null)
        {
            SetPreviewMessage($"미리보기 지원 형식이 아닙니다.\n{System.IO.Path.GetFileName(path)}");
            return;
        }

        SetPreviewMessage("불러오는 중…");
        try
        {
            var element = await provider.CreatePreviewAsync(path, ct);
            if (ct.IsCancellationRequested)
            {
                return;   // 다른 선택으로 대체됨
            }
            PreviewHost.Child = element ?? MessageBlock("미리보기를 만들 수 없습니다.");
        }
        catch (OperationCanceledException)
        {
            // 취소는 무시
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                SetPreviewMessage($"미리보기 실패: {ex.Message}");
            }
        }
    }

    private void SetPreviewMessage(string text) => PreviewHost.Child = MessageBlock(text);

    private static TextBlock MessageBlock(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.6,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
    };
}
