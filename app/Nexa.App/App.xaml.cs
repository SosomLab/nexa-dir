using Microsoft.UI.Xaml;

namespace Nexa.App;

/// <summary>애플리케이션 진입점. 메인 윈도우를 생성한다.</summary>
public partial class App : Application
{
    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
