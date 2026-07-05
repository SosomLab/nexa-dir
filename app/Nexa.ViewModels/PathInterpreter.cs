using System;
using System.Text.RegularExpressions;

namespace Nexa.ViewModels;

/// <summary>
/// 경로 입력 해석 레이어(UI 비종속 순수 로직) — 사용자가 경로 바에 붙여넣거나 입력한 문자열을
/// <b>실제 파일시스템 경로로 확장</b>한다. 지원:
/// <list type="bullet">
///   <item>CMD 스타일 환경변수 <c>%USERPROFILE%</c>, <c>%APPDATA%</c> …</item>
///   <item>PowerShell 스타일 <c>$env:USERPROFILE</c>, <c>${env:ProgramFiles(x86)}</c> …</item>
///   <item>감싸는 따옴표/공백 제거(복사 시 흔한 <c>"C:\..."</c>).</item>
/// </list>
/// 정의되지 않은 변수는 <b>원문 그대로 유지</b>(경로 존재 검사에서 자연히 실패 → "경로 없음"). 맥/Windows 공통 테스트 대상.
/// </summary>
public static class PathInterpreter
{
    // PowerShell: ${env:NAME} (중괄호 안은 ) 등 특수문자 허용) — 이름은 } 전까지.
    private static readonly Regex PsBraced = new(
        @"\$\{env:(?<name>[^}]+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // PowerShell: $env:NAME (중괄호 없음 → 이름은 단어문자만, PowerShell 파싱과 동일).
    private static readonly Regex PsBare = new(
        @"\$env:(?<name>[A-Za-z0-9_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>환경변수(CMD <c>%VAR%</c> · PowerShell <c>$env:VAR</c>/<c>${env:VAR}</c>)를 확장한 경로를 반환.
    /// 미정의 변수는 원문 유지. 앞뒤 따옴표·공백은 제거.</summary>
    public static string Expand(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }
        string s = input.Trim();

        // 붙여넣기 시 흔한 감싸는 따옴표 제거("..." 또는 '...').
        if (s.Length >= 2 &&
            ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
        {
            s = s[1..^1];
        }

        // PowerShell: ${env:NAME} → 값(미정의는 원문 유지). 중괄호형을 먼저(더 구체적).
        s = PsBraced.Replace(s, m =>
            Environment.GetEnvironmentVariable(m.Groups["name"].Value) ?? m.Value);
        // PowerShell: $env:NAME → 값(미정의는 원문 유지).
        s = PsBare.Replace(s, m =>
            Environment.GetEnvironmentVariable(m.Groups["name"].Value) ?? m.Value);

        // CMD: %NAME% → 값(미정의는 %NAME% 원문 유지). .NET 기본 확장 사용.
        s = Environment.ExpandEnvironmentVariables(s);

        return s.Trim();
    }
}
