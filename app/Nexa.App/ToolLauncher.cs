using System;
using System.Diagnostics;
using System.IO;

namespace Nexa.App;

/// <summary>
/// 도구 모음(내장 액션의 외부 실행부)·퀵 런처(외부 프로그램)의 <b>프로세스 실행 헬퍼</b>(도구 슬라이스 1, docs/44).
/// 모든 실행은 <b>실패를 격리</b>(오류 격리 NFR) — 미설치/권한 실패는 false 반환, 호출측이 상태바로 안내.
/// </summary>
internal static class ToolLauncher
{
    /// <summary>현재 폴더에서 <b>외부 터미널</b> 열기 — Windows Terminal(wt) 우선, 없으면 PowerShell 7 → Windows PowerShell → cmd 폴백.</summary>
    public static bool OpenTerminal(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return false;
        }
        // Windows Terminal은 앱 실행 별칭(WindowsApps)이라 ShellExecute로 해석. -d=시작 디렉터리.
        if (TryStart("wt.exe", $"-d \"{folder}\"", null))
        {
            return true;
        }
        // 폴백: 콘솔 셸을 작업 디렉터리에서 새 창으로.
        return TryStart("pwsh.exe", null, folder)
            || TryStart("powershell.exe", null, folder)
            || TryStart("cmd.exe", null, folder);
    }

    /// <summary>외부 프로그램 실행 — 인자 템플릿의 <c>%path%</c>를 현재 폴더로 치환. UseShellExecute(경로/확장자 연결).</summary>
    public static bool Launch(string exe, string? argsTemplate, string folder)
    {
        if (string.IsNullOrEmpty(exe))
        {
            return false;
        }
        string args = (argsTemplate ?? string.Empty).Replace("%path%", folder);
        return TryStart(exe, args, Directory.Exists(folder) ? folder : null);
    }

    private static bool TryStart(string file, string? args, string? workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = file, UseShellExecute = true };
            if (!string.IsNullOrEmpty(args))
            {
                psi.Arguments = args;
            }
            if (!string.IsNullOrEmpty(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;   // 미설치/실패 → 다음 폴백 또는 호출측 안내
        }
    }

    /// <summary>VS Code 실행 파일(<c>Code.exe</c>) 경로 추정 — 사용자/시스템 설치 공통 위치. 없으면 null(런처 시드 제외).</summary>
    public static string? ResolveVsCode()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] candidates =
        {
            Path.Combine(local, "Programs", "Microsoft VS Code", "Code.exe"),   // 사용자 설치(가장 흔함)
            Path.Combine(pf, "Microsoft VS Code", "Code.exe"),                   // 시스템 설치(x64)
            Path.Combine(pfx86, "Microsoft VS Code", "Code.exe"),               // 시스템 설치(x86)
        };
        foreach (string c in candidates)
        {
            if (File.Exists(c))
            {
                return c;
            }
        }
        return null;
    }
}
