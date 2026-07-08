using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nexa.ViewModels;

/// <summary>
/// 순수 로컬라이저(i18n, D-2/PREF-8) — 문자열 테이블(키→번역)과 조회. UI 비종속이라 맥/Win xUnit 대상.
/// 조회 폴백: <b>현재 문화 → 폴백(en) → 키 자체</b>(누락 키가 화면에서 최소한 식별 가능). 앱은 JSON 테이블을
/// 로드해 <see cref="Current"/>에 주입하고, XAML은 마크업 확장/코드가 <see cref="T(string)"/>로 조회한다.
/// </summary>
public sealed class Localizer
{
    private readonly IReadOnlyDictionary<string, string> _table;
    private readonly IReadOnlyDictionary<string, string>? _fallback;

    /// <summary>현재 문화 코드(예: "ko", "en"). 표시/저장 참고용.</summary>
    public string Culture { get; }

    public Localizer(string culture, IReadOnlyDictionary<string, string> table,
        IReadOnlyDictionary<string, string>? fallback = null)
    {
        Culture = culture ?? string.Empty;
        _table = table ?? new Dictionary<string, string>();
        _fallback = fallback;
    }

    /// <summary>전역 현재 로컬라이저 — 기본은 빈 테이블(키 그대로 반환). 앱 시작 시 실제 테이블로 교체.</summary>
    public static Localizer Current { get; private set; } =
        new(string.Empty, new Dictionary<string, string>());

    public static void SetCurrent(Localizer localizer) => Current = localizer ?? Current;

    /// <summary>키의 번역 — 현재 테이블 → 폴백 → 키 자체. null 키는 빈 문자열.</summary>
    public string T(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }
        if (_table.TryGetValue(key, out string? v))
        {
            return v;
        }
        if (_fallback is not null && _fallback.TryGetValue(key, out string? f))
        {
            return f;
        }
        return key;   // 누락 → 키 그대로(개발 중 식별 용이)
    }

    /// <summary><see cref="T(string)"/> 결과를 <see cref="string.Format(string,object[])"/>로 채운다(불변 문화).</summary>
    public string T(string key, params object[] args)
    {
        string fmt = T(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, fmt, args);
        }
        catch (FormatException)
        {
            return fmt;   // 포맷 자리표 불일치 → 원문(런타임 예외 방지)
        }
    }
}
