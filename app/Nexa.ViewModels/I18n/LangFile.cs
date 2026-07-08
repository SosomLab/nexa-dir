using System.Collections.Generic;

namespace Nexa.ViewModels.I18n;

/// <summary>
/// 파싱된 언어 파일 1개 — 메타데이터(<c>@</c> 접두 키)와 문자열 테이블을 분리 보관(i18n, docs/42).
/// 포맷(JSON/properties)에 <b>무관한 중립 표현</b>이라 <see cref="ILangFormat"/> 구현이 무엇이든 동일 결과.
/// </summary>
public sealed class LangFile
{
    /// <summary><c>@</c> 접두 메타(예: <c>@code</c>, <c>@name</c>) — 접두 포함 원형 키로 보관.</summary>
    public IReadOnlyDictionary<string, string> Meta { get; }

    /// <summary>실제 문자열 항목(키→번역) — <c>@</c> 비접두.</summary>
    public IReadOnlyDictionary<string, string> Strings { get; }

    public LangFile(IReadOnlyDictionary<string, string> meta, IReadOnlyDictionary<string, string> strings)
    {
        Meta = meta ?? new Dictionary<string, string>();
        Strings = strings ?? new Dictionary<string, string>();
    }

    private string M(string key, string fallback = "") => Meta.TryGetValue(key, out string? v) ? v : fallback;

    /// <summary>BCP-47 코드(예: ko, en). 없으면 빈 문자열 — 카탈로그가 파일명으로 보정.</summary>
    public string Code => M("@code");
    /// <summary>자기 언어 표기(설정 목록 노출용).</summary>
    public string Name => M("@name");
    /// <summary>영어 표기(중립 식별/정렬용).</summary>
    public string NameEn => M("@name.en");
    /// <summary>번역 작성자.</summary>
    public string Author => M("@author");
    /// <summary>대상 앱 버전(구버전 번역 경고용).</summary>
    public string App => M("@app");
    /// <summary>이 언어에서 누락 시 참조 코드(기본 en).</summary>
    public string Fallback => M("@fallback", "en");

    /// <summary>
    /// 여러 <see cref="LangFile"/>의 문자열을 <b>낮은 우선 → 높은 우선</b> 순으로 병합(뒤가 이김).
    /// 사용자 오버라이드 파일이 설치본 위로 키 단위 덮어쓰기(docs/42 §2).
    /// </summary>
    public static Dictionary<string, string> MergeStrings(IEnumerable<LangFile> lowToHigh)
    {
        var merged = new Dictionary<string, string>();
        foreach (LangFile f in lowToHigh)
        {
            if (f is null)
            {
                continue;
            }
            foreach (var kv in f.Strings)
            {
                merged[kv.Key] = kv.Value;   // 뒤(높은 우선)가 앞을 덮음
            }
        }
        return merged;
    }
}
