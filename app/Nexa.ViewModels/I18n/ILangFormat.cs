namespace Nexa.ViewModels.I18n;

/// <summary>
/// 언어 파일 텍스트 → <see cref="LangFile"/> 파서 계약(i18n, docs/42 §3-4).
/// 포맷을 <b>교체 가능한 심(seam)</b>으로 분리 — 현재 활성=JSON(<see cref="JsonLangFormat"/>),
/// properties(<see cref="PropertiesLangFormat"/>)로 전환은 <see cref="LangFormats.Active"/> 한 줄 교체.
/// (UDF 엔진 추상화 [docs/41 §0]와 동일 설계 패턴.)
/// </summary>
public interface ILangFormat
{
    /// <summary>포맷 식별자("json"/"properties") — 설정·진단·전환 지정용.</summary>
    string Id { get; }

    /// <summary>파일 텍스트를 파싱(실패/파손은 최대한 부분 복구, 치명 오류만 예외).</summary>
    LangFile Parse(string content);
}
