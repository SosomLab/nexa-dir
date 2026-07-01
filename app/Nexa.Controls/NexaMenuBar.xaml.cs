using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nexa.Controls;

/// <summary>
/// 초박형 커스텀 메뉴 바(재사용 컨트롤). 최상위 메뉴는 <see cref="Button"/>, 드롭다운은 <see cref="MenuFlyout"/>.
/// 기본 <c>MenuBar</c>가 내부 템플릿 때문에 18px급으로 못 줄어드는 문제(글자 잘림)를 우회한다.
/// 높이=<see cref="MenuHeight"/>, 글자=<c>FontSize</c>로 완전 제어. 명령 연결은 후속.
/// </summary>
public sealed partial class NexaMenuBar : UserControl
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    public NexaMenuBar()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    /// <summary>최상위 메뉴 목록(XAML에서 <c>NexaMenuBar.Menus</c>로 채운다).</summary>
    public IList<NexaMenu> Menus { get; } = new List<NexaMenu>();

    /// <summary>메뉴 바(최상위 버튼) 높이(px). 기본 22, 초박형은 18 등.</summary>
    public static readonly DependencyProperty MenuHeightProperty =
        DependencyProperty.Register(
            nameof(MenuHeight), typeof(double), typeof(NexaMenuBar), new PropertyMetadata(22.0));

    public double MenuHeight
    {
        get => (double)GetValue(MenuHeightProperty);
        set => SetValue(MenuHeightProperty, value);
    }

    /// <summary>Menus로부터 최상위 버튼 + 드롭다운을 생성한다(Loaded 시점).</summary>
    private void Build()
    {
        Host.Children.Clear();
        foreach (var menu in Menus)
        {
            MenuFlyout? flyout = null;
            if (menu.Items.Count > 0)
            {
                flyout = new MenuFlyout();
                foreach (var entry in menu.Items)
                {
                    flyout.Items.Add(new MenuFlyoutItem { Text = entry.Text });
                }
            }

            var btn = new Button
            {
                Content = menu.Header,
                FontSize = FontSize,
                Height = MenuHeight,
                MinHeight = 0,
                MinWidth = 0,
                Padding = new Thickness(8, 0, 8, 0),
                Background = TransparentBrush,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                VerticalAlignment = VerticalAlignment.Stretch,
                Flyout = flyout,
            };
            Host.Children.Add(btn);
        }
    }
}
