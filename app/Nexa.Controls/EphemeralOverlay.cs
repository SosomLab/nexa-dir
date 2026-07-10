using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nexa.Controls;

/// <summary>
/// 휘발성 텍스트 HUD(도메인 비종속, docs/32 §7-A) — 타입어헤드 검색어 등 <b>순간 피드백</b>을
/// 목록 위 플로팅 배지로 표시하고 <see cref="Timeout"/> 후 자동 소거한다. 오버레이라 레이아웃을
/// 밀지 않으며(reflow 없음) 히트테스트 불가. 재사용: "복사됨"·드롭 힌트 등 어떤 순간 알림에도
/// <see cref="Show"/> 호출만으로 사용.
/// </summary>
public sealed partial class EphemeralOverlay : UserControl
{
    private readonly TextBlock _text;
    private readonly DispatcherTimer _timer = new();

    /// <summary>마지막 <see cref="Show"/> 후 자동 소거까지 시간(기본 1초). 호스트가 설정과 동기
    /// (타입어헤드는 버퍼 리셋 타임아웃과 같은 값 — 버퍼가 사라질 때 표시도 사라짐).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);

    public EphemeralOverlay()
    {
        _text = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xF4, 0xF7)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var icon = new FontIcon
        {
            Glyph = "",   // Search
            FontSize = 12,
            Foreground = _text.Foreground,
            Margin = new Thickness(0, 0, 6, 0),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(icon);
        row.Children.Add(_text);
        Content = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xD9, 0x1F, 0x26, 0x30)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 5, 10, 5),
            Child = row,
        };
        IsHitTestVisible = false;   // 표시 전용 — 아래 목록의 클릭/드래그 방해 금지
        IsTabStop = false;
        Visibility = Visibility.Collapsed;
        _timer.Tick += (_, _) => Clear();
    }

    /// <summary>텍스트를 설정하고 표시 — 자동 소거 타이머 리셋(연속 호출 중엔 계속 표시).</summary>
    public void Show(string text)
    {
        _text.Text = text;
        Visibility = Visibility.Visible;
        _timer.Stop();
        _timer.Interval = Timeout;
        _timer.Start();
    }

    /// <summary>즉시 소거.</summary>
    public void Clear()
    {
        _timer.Stop();
        Visibility = Visibility.Collapsed;
    }
}
