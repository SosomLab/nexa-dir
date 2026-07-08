using System.Collections.Generic;
using System.Text.Json;

namespace Nexa.ViewModels.I18n;

/// <summary>
/// JSON 언어 포맷(현재 활성, docs/42 §3) — 평탄 객체 <c>{ "키": "값" }</c>.
/// <c>@</c> 접두 키는 메타데이터(<c>@code</c>·<c>@name</c>…), 나머지는 문자열.
/// properties 포맷과 <b>동일한 키 모델</b>(@meta + key)이라 <see cref="LangFile"/> 결과가 대등 → 전환 무손실.
/// </summary>
public sealed class JsonLangFormat : ILangFormat
{
    public string Id => "json";

    public LangFile Parse(string content)
    {
        var meta = new Dictionary<string, string>();
        var strings = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new LangFile(meta, strings);
        }

        // 값이 문자열이 아닐 수도 있으므로 관용적으로 JsonElement로 읽어 문자열화(비문자열은 스킵).
        Dictionary<string, JsonElement>? raw =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
        if (raw is null)
        {
            return new LangFile(meta, strings);
        }
        foreach (var kv in raw)
        {
            if (kv.Value.ValueKind != JsonValueKind.String)
            {
                continue;   // 문자열 값만 채택(구조 오염 방지)
            }
            string val = kv.Value.GetString() ?? string.Empty;
            if (kv.Key.Length > 0 && kv.Key[0] == '@')
            {
                meta[kv.Key] = val;
            }
            else
            {
                strings[kv.Key] = val;
            }
        }
        return new LangFile(meta, strings);
    }
}
