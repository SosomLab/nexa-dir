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
/// 앱 i18n 부트스트랩(D-2/PREF-8, docs/42) — <b>외부 언어 파일</b>(<see cref="LangCatalog"/>: 설치 <c>lang/</c> +
/// 사용자 <c>%APPDATA%/NexaDir/lang/</c>)을 로드해 <see cref="Localizer.Current"/>에 주입한다.
/// 재빌드 없이 언어 추가·수정 가능(포맷=JSON, <c>LangFormats.Active</c>로 properties 전환).
/// <para>파일이 없거나 실패하면 <b>임베디드 en</b>(<see cref="EmbeddedTable"/>)이 최후 안전망 — UI 붕괴 방지.
/// 기준(base) 언어=영어. 마크업 확장은 파싱 시점 정적 조회라 <see cref="Init"/>는 첫 창 생성 전 1회,
/// 언어 변경은 재시작 반영(코드 <see cref="T"/>는 즉시).</para>
/// </summary>
internal static class Loc
{
    /// <summary>임베디드 안전망 코드(파일 폴더가 통째로 없을 때 대비) — 기준 언어 en만 유지.</summary>
    private static readonly string[] EmbeddedCodes = { "en" };

    /// <summary>설정(문화 코드; ""=시스템)을 해석해 Localizer.Current 초기화. 첫 창 생성 전 1회.</summary>
    public static void Init(string cultureSetting)
    {
        IReadOnlyList<LangInfo> catalog = LangCatalog.Discover();
        string code = Resolve(cultureSetting, catalog);

        var table = LangCatalog.Load(code) ?? EmbeddedTable(code) ?? new Dictionary<string, string>();
        Dictionary<string, string>? fallback = code == "en"
            ? null
            : (LangCatalog.Load("en") ?? EmbeddedTable("en"));

        Localizer.SetCurrent(new Localizer(code, table, fallback));
    }

    /// <summary>코드 조회 단축(상태바·대화상자 등). 자리표는 args로.</summary>
    public static string T(string key) => Localizer.Current.T(key);
    public static string T(string key, params object[] args) => Localizer.Current.T(key, args);

    /// <summary>설정값이 가리키는 언어가 <b>현재 적용 중</b> 언어와 다른가 — 재시작 필요 판정(PREF-9).
    /// ""(시스템)·미지원 코드도 <see cref="Resolve"/>로 실코드화해 비교하므로 원복 시 false.</summary>
    public static bool IsPendingCultureChange(string cultureSetting) =>
        !string.Equals(Resolve(cultureSetting, LangCatalog.Discover()), Localizer.Current.Culture,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>설정값("", "ko", "en"…) → 실제 코드. ""=시스템 UI 문화 2글자. 미지원(발견·임베디드 모두 없음)=en.</summary>
    private static string Resolve(string setting, IReadOnlyList<LangInfo> catalog)
    {
        bool Has(string c)
        {
            if (Array.IndexOf(EmbeddedCodes, c) >= 0)
            {
                return true;
            }
            foreach (LangInfo i in catalog)
            {
                if (string.Equals(i.Code, c, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        if (!string.IsNullOrEmpty(setting))
        {
            return Has(setting) ? setting : "en";
        }
        string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Has(two) ? two : "en";
    }

    /// <summary>임베디드 안전망 리소스 <c>Nexa.App.Strings.{code}.json</c>(en만) 로드(없으면 null).</summary>
    private static Dictionary<string, string>? EmbeddedTable(string code)
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
