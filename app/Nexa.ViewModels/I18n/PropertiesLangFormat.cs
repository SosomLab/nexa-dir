using System.Collections.Generic;
using System.Text;

namespace Nexa.ViewModels.I18n;

/// <summary>
/// properties 스타일 언어 포맷(전환 대상, docs/42 §3) — 라인 기반 <c>키 = 값</c>,
/// <c>#</c> 주석·빈 줄 무시, <c>@</c> 접두 키=메타. 값 이스케이프 <c>\n \t \\</c>.
/// <see cref="JsonLangFormat"/>과 동일한 키 모델이라 <see cref="LangFormats.Active"/> 교체만으로 전환.
/// </summary>
public sealed class PropertiesLangFormat : ILangFormat
{
    public string Id => "properties";

    public LangFile Parse(string content)
    {
        var meta = new Dictionary<string, string>();
        var strings = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(content))
        {
            return new LangFile(meta, strings);
        }

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;   // 빈 줄·주석
            }
            int eq = line.IndexOf('=');
            if (eq < 0)
            {
                continue;   // '=' 없는 줄 스킵(파손 격리)
            }
            string key = line.Substring(0, eq).Trim();
            if (key.Length == 0)
            {
                continue;
            }
            string val = Unescape(line.Substring(eq + 1).Trim());
            if (key[0] == '@')
            {
                meta[key] = val;
            }
            else
            {
                strings[key] = val;
            }
        }
        return new LangFile(meta, strings);
    }

    /// <summary><c>\n</c>(개행)·<c>\t</c>(탭)·<c>\\</c>(역슬래시) 해제. 그 외 <c>\x</c>는 리터럴 x.</summary>
    private static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0)
        {
            return s;
        }
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n switch { 'n' => '\n', 't' => '\t', '\\' => '\\', _ => n });
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
