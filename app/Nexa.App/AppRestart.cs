using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace Nexa.App;

/// <summary>
/// 앱 자체 재시작(PREF-9, docs/40 §9) — 언어 등 <b>재시작 필요 설정</b> 변경 승인 시 사용.
/// 1차 = Windows App SDK <c>AppInstance.Restart</c>(패키지/미패키지 공용; 성공하면 복귀하지 않음),
/// 2차 폴백 = 현재 exe 재기동 + <see cref="Application.Exit"/>(런타임 미지원·실패 대비, 포터블 배포 안전망).
/// <para>호출 전 반드시 영속 상태(settings/session) flush를 끝낼 것 — Restart는 프로세스를 즉시 종료해
/// <c>Closed</c>/<c>ProcessExit</c> 훅 실행을 보장하지 않는다(MainWindow.RestartApp이 담당).</para>
/// </summary>
internal static class AppRestart
{
    /// <summary>앱을 재시작한다. 성공 시 이 호출은 복귀하지 않는다.</summary>
    public static void Restart()
    {
        try
        {
            // 성공하면 여기서 프로세스 종료·재기동. 복귀했다면 실패한 것(사유 무관 폴백 진행).
            Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
        }
        catch
        {
            // API 자체 사용 불가(런타임 구성 등) → 폴백.
        }
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory });
            }
        }
        catch
        {
            // 새 인스턴스 기동 실패 — 종료만이라도 진행하지 않고 현 인스턴스 유지가 낫다.
            return;
        }
        Application.Current.Exit();
    }
}
