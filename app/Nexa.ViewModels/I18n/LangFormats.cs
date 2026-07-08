using System.Collections.Generic;

namespace Nexa.ViewModels.I18n;

/// <summary>
/// 언어 파일 포맷 레지스트리 + <b>단일 전환 지점</b>(i18n, docs/42 §3).
/// <see cref="Active"/> 한 줄을 <see cref="PropertiesLangFormat"/>로 바꾸면 전체 파서가 전환된다
/// (파일 확장자는 <c>.lang</c>로 포맷 무관 — 내용만 해당 포맷으로 교체).
/// </summary>
public static class LangFormats
{
    /// <summary>현재 활성 포맷 — <b>JSON</b>. properties 전환은 이 대입만 교체.</summary>
    public static ILangFormat Active { get; set; } = new JsonLangFormat();

    /// <summary>사용 가능한 전 포맷(진단·명시 선택용).</summary>
    public static readonly IReadOnlyList<ILangFormat> All = new ILangFormat[]
    {
        new JsonLangFormat(),
        new PropertiesLangFormat(),
    };

    /// <summary>식별자로 포맷 조회(없으면 <see cref="Active"/>).</summary>
    public static ILangFormat ById(string id)
    {
        foreach (ILangFormat f in All)
        {
            if (f.Id == id)
            {
                return f;
            }
        }
        return Active;
    }
}
