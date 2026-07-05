using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Nexa.App;

/// <summary>하단 도킹 패널이 호스팅할 콘텐츠 종류(BP-1). 미리보기/Hex/터미널은 후속 구현.</summary>
public enum BottomPanelKind
{
    Info,
    Preview,
    Hex,
    Terminal,
}

/// <summary>
/// 하단 도킹 패널의 <b>콘텐츠 호스트</b>(재사용 — 좌/우 도킹에 각각 배치). 종류 선택(정보/미리보기/Hex/터미널)에
/// 따라 콘텐츠를 스왑한다(BP-1a). 현재는 <b>정보</b>만 실제(<see cref="InfoText"/>), 나머지는 "준비 중".
/// 터미널(ConPTY)·미리보기·Hex는 후속 슬라이스(BP-2/BP-T).
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
            }
        }
    }

    private void OnKindClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag && Enum.TryParse<BottomPanelKind>(tag, out var k))
        {
            Kind = k;
        }
        SyncToggles();   // 라디오식: 항상 선택 하나 유지(이미 선택된 것 재클릭도 체크 유지)
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
        ContentText.Text = _kind switch
        {
            BottomPanelKind.Info => string.IsNullOrEmpty(InfoText) ? "(정보 없음)" : InfoText,
            BottomPanelKind.Preview => "미리보기 — 준비 중 (후속 구현)",
            BottomPanelKind.Hex => "Hex 뷰 — 준비 중 (후속 구현)",
            BottomPanelKind.Terminal => "터미널(ConPTY) — 준비 중 (후속 구현)",
            _ => string.Empty,
        };
    }
}
