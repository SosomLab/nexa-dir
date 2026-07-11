using System;
using System.Collections.Generic;

namespace Nexa.ViewModels;

/// <summary>
/// 경로바 편집 자동완성(탐색기식) — 입력 텍스트를 <b>베이스 폴더 + 마지막 세그먼트 접두사</b>로 분해해
/// 일치하는 하위 폴더 전체 경로 목록을 만든다. 순수 로직(IO는 열거자 주입) — 맥/Win xUnit 대상.
/// 사용: 구분자('\\'/'/')를 입력하면 그 폴더의 전체 목록, 이어서 타이핑하면 접두사 필터.
/// </summary>
public static class PathSuggestions
{
    /// <summary>
    /// <paramref name="text"/>에 대한 폴더 제안 목록(전체 경로, 열거 순서 유지, 최대 <paramref name="max"/>개).
    /// 구분자가 없거나 베이스 폴더 열거가 실패하면(없음/권한) 빈 목록 — 호출측은 팝업을 닫으면 된다.
    /// </summary>
    /// <param name="enumerateDirs">베이스 폴더의 하위 폴더 전체 경로 열거(예: Directory.EnumerateDirectories).</param>
    public static IReadOnlyList<string> SuggestFolders(
        string? text, Func<string, IEnumerable<string>> enumerateDirs, int max = 20)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || max <= 0)
        {
            return result;
        }
        int lastSep = text.LastIndexOfAny(new[] { '\\', '/' });
        if (lastSep < 0)
        {
            return result;   // "C:" 등 구분자 이전 단계(드라이브 제안)는 후속
        }
        string baseDir = text[..(lastSep + 1)];   // 구분자 포함("C:\Users\")
        string prefix = text[(lastSep + 1)..];
        try
        {
            foreach (string dir in enumerateDirs(baseDir))
            {
                if (LastName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(dir);
                    if (result.Count >= max)
                    {
                        break;
                    }
                }
            }
        }
        catch
        {
            result.Clear();   // 존재하지 않는 폴더·권한 거부 등 — 제안 없음
        }
        return result;
    }

    /// <summary>경로의 마지막 세그먼트(폴더명). 끝 구분자는 무시, 양쪽 구분자('\\'/'/') 처리.</summary>
    private static string LastName(string path)
    {
        var span = path.AsSpan().TrimEnd('\\').TrimEnd('/');
        int i = span.LastIndexOfAny('\\', '/');
        return span[(i + 1)..].ToString();
    }
}
