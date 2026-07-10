using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Nexa.Plugins.Preview;
using Nexa.Plugins.Samples;

namespace Nexa.App;

/// <summary>애플리케이션 진입점. 메인 윈도우를 생성한다.</summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        // 내장 미리보기 공급자 등록(BP-2) — 샘플 플러그인(Nexa.Plugins.Samples)을 그대로 dogfooding.
        // 플러그인은 나중에 등록되면 우선권을 갖는다(PreviewRegistry).
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
            // 설정 로드는 여기 1회(콜드 스타트 파일 I/O+역직렬화 중복 제거, 감사 004) —
            // 인메모리 적용까지 끝내 MainWindow는 AppSettings만 사용. i18n은 창 생성 전(마크업 확장이 파싱 시점 조회).
            var loaded = SettingsStore.Load(SettingsStore.DefaultPath());
            SettingsStore.Apply(loaded);
            Loc.Init(loaded?.General.Culture ?? string.Empty);

            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex);
            throw;   // 창 생성 자체가 실패하면 표시할 UI가 없음 — 로그 후 전파
        }
    }

    /// <summary>미처리 예외를 크래시 로그에 기록(경로: %LOCALAPPDATA%\NexaDir\crash.log,
    /// 포터블=exe\data\crash.log). 실패해도 무시.</summary>
    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            string dir = AppPaths.LocalRoot;
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
