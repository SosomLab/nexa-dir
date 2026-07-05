using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Nexa.App.Preview;
using Nexa.Plugins.Preview;

namespace Nexa.App;

/// <summary>애플리케이션 진입점. 메인 윈도우를 생성한다.</summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        // 내장 미리보기 공급자 등록(BP-2). 플러그인은 나중에 등록되면 우선권을 갖는다(PreviewRegistry).
        PreviewRegistry.Register(new ImagePreviewProvider());
        PreviewRegistry.Register(new TextPreviewProvider());
        // 오류 격리(NFR-R3): 미처리 예외를 조용히 종료시키지 말고 로그로 남긴다.
        // 로그: %LOCALAPPDATA%\NexaDir\crash.log
        UnhandledException += (_, e) =>
        {
            LogCrash("UI", e.Exception);
            e.Handled = true;   // UI 스레드 예외는 삼켜 앱 생존(가능한 경우)
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { LogCrash("Task", e.Exception); e.SetObserved(); };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex);
            throw;   // 창 생성 자체가 실패하면 표시할 UI가 없음 — 로그 후 전파
        }
    }

    /// <summary>미처리 예외를 크래시 로그에 기록(경로: %LOCALAPPDATA%\NexaDir\crash.log). 실패해도 무시.</summary>
    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexaDir");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}\n{ex}\n\n");
        }
        catch
        {
            // 로깅 실패는 무시(로깅이 크래시를 만들지 않도록).
        }
    }
}
