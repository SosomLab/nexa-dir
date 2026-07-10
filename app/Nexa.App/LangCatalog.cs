using System;
using System.Collections.Generic;
using System.IO;
using Nexa.ViewModels.I18n;

namespace Nexa.App;

/// <summary>발견된 언어 1개(설정 목록 표시용) — 코드 + 표기 + 대상 앱 버전.</summary>
internal sealed record LangInfo(string Code, string Name, string NameEn, string App);

/// <summary>
/// 외부 언어 파일(<c>.lang</c>) 탐색·로드(i18n, docs/42) — 두 폴더를 키 단위로 병합.
/// <list type="number">
///   <item>설치 폴더 <c>&lt;exe&gt;/lang/</c> — 배포 기본 언어(업데이트로 갱신)</item>
///   <item>사용자 폴더 <c>%APPDATA%/NexaDir/lang/</c> — 추가·오버라이드(업데이트가 안 지움, 우선)</item>
/// </list>
/// 파싱은 <see cref="LangFormats.Active"/>(현재 JSON). 폴더가 없거나 실패하면 빈 결과 →
/// 호출측(<see cref="Loc"/>)이 임베디드 en 안전망으로 폴백.
/// </summary>
internal static class LangCatalog
{
    /// <summary>설치 폴더 <c>lang/</c>(exe 옆). 배포 산출물.</summary>
    private static string InstallDir => Path.Combine(AppContext.BaseDirectory, "lang");

    /// <summary>사용자 폴더 <c>%APPDATA%/NexaDir/lang/</c>(포터블=<c>exe/data/lang/</c>). 추가·오버라이드용(우선).</summary>
    private static string UserDir => Path.Combine(AppPaths.RoamingRoot, "lang");

    /// <summary>병합 우선 순서(낮음→높음): 설치 → 사용자(뒤가 이김).</summary>
    private static IEnumerable<string> DirsLowToHigh()
    {
        yield return InstallDir;
        yield return UserDir;
    }

    /// <summary>
    /// 발견된 언어 목록(코드별 1개). 사용자 폴더의 표기/메타가 설치본을 덮는다.
    /// 설정 "언어" 페이지 라디오의 원천(하드코딩 대체).
    /// </summary>
    public static IReadOnlyList<LangInfo> Discover()
    {
        var byCode = new Dictionary<string, LangInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in DirsLowToHigh())   // 낮음→높음: 뒤(사용자)가 앞을 덮음
        {
            foreach (string path in EnumerateLangFiles(dir))
            {
                LangFile? lf = TryParse(path);
                if (lf is null)
                {
                    continue;
                }
                string code = string.IsNullOrEmpty(lf.Code)
                    ? Path.GetFileNameWithoutExtension(path)   // @code 없으면 파일명으로 보정
                    : lf.Code;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }
                string name = string.IsNullOrEmpty(lf.Name) ? code : lf.Name;
                byCode[code] = new LangInfo(code, name, lf.NameEn, lf.App);
            }
        }
        var list = new List<LangInfo>(byCode.Values);
        list.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
        return list;
    }

    /// <summary>
    /// 한 언어의 문자열 테이블 — 설치 + 사용자 파일을 <b>키 단위 병합</b>(사용자 우선).
    /// 해당 코드 파일이 하나도 없으면 null(호출측이 폴백).
    /// </summary>
    public static Dictionary<string, string>? Load(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }
        var files = new List<LangFile>();
        foreach (string dir in DirsLowToHigh())   // 설치(낮음) → 사용자(높음)
        {
            LangFile? lf = TryParse(Path.Combine(dir, code + ".lang"));
            if (lf is not null)
            {
                files.Add(lf);
            }
        }
        return files.Count == 0 ? null : LangFile.MergeStrings(files);
    }

    private static IEnumerable<string> EnumerateLangFiles(string dir)
    {
        string[] files;
        try
        {
            files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.lang") : Array.Empty<string>();
        }
        catch
        {
            files = Array.Empty<string>();
        }
        return files;
    }

    /// <summary>파일을 활성 포맷으로 파싱(없거나 실패=null, 격리).</summary>
    private static LangFile? TryParse(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            // UTF-8(BOM 있으면 자동 스킵). CJK 필수.
            string text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return LangFormats.Active.Parse(text);
        }
        catch
        {
            return null;
        }
    }
}
