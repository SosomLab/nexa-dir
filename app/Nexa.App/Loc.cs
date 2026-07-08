using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.UI.Xaml.Markup;
using Nexa.ViewModels;

namespace Nexa.App;

/// <summary>
/// 앱 i18n 부트스트랩(D-2/PREF-8) — 임베디드 JSON 문자열 테이블(Strings/{code}.json)을 로드해
/// <see cref="Localizer.Current"/>에 주입한다. 지원 언어: 한국어(ko)·영어(en). 폴백=en.
/// XAML은 <see cref="LocExtension"/>(<c>{loc:Loc Key=...}</c>)로, 코드는 <see cref="T"/>로 조회.
/// <para><b>마크업 확장은 XAML 파싱 시점(정적)에 평가</b>되므로 <see cref="Init"/>는 첫 창 생성 전에 호출해야 하고,
/// 언어 변경은 재시작 후 완전히 반영된다(코드 조회는 즉시).</para>
/// </summary>
internal static class Loc
{
    public static readonly string[] Supported = { "ko", "en" };

    /// <summary>설정(문화 코드; ""=시스템)을 해석해 Localizer.Current 초기화. 첫 창 생성 전 1회.</summary>
    public static void Init(string cultureSetting)
    {
        string code = Resolve(cultureSetting);
        var table = LoadTable(code) ?? new Dictionary<string, string>();
        var fallback = code == "en" ? null : LoadTable("en");
        Localizer.SetCurrent(new Localizer(code, table, fallback));
    }

    /// <summary>코드 조회 단축(상태바·대화상자 등). 자리표는 args로.</summary>
    public static string T(string key) => Localizer.Current.T(key);
    public static string T(string key, params object[] args) => Localizer.Current.T(key, args);

    /// <summary>설정값("", "ko", "en") → 실제 코드. ""=시스템 UI 문화의 2글자, 미지원이면 en.</summary>
    private static string Resolve(string setting)
    {
        if (!string.IsNullOrEmpty(setting))
        {
            return Array.IndexOf(Supported, setting) >= 0 ? setting : "en";
        }
        string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Array.IndexOf(Supported, two) >= 0 ? two : "en";
    }

    /// <summary>임베디드 리소스 <c>Nexa.App.Strings.{code}.json</c>를 평탄 딕셔너리로 로드(실패=null).</summary>
    private static Dictionary<string, string>? LoadTable(string code)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string res = $"Nexa.App.Strings.{code}.json";
            using Stream? s = asm.GetManifestResourceStream(res);
            if (s is null)
            {
                return null;
            }
            using var r = new StreamReader(s);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(r.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>XAML 문자열 지역화 마크업 확장 — <c>Header="{loc:Loc Key=menu.file}"</c>. 파싱 시점 정적 조회.</summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    /// <summary>문자열 테이블 키.</summary>
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => Localizer.Current.T(Key);
}
