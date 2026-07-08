using System.Collections.Generic;
using Nexa.ViewModels.I18n;
using Xunit;

namespace Nexa.ViewModels.Tests;

/// <summary>언어 파일 포맷(JSON/properties) 파싱·메타 분리·병합·포맷 대등성(docs/42) 단위 테스트.</summary>
public sealed class LangFormatTests
{
    [Fact]
    public void Json_separates_meta_and_strings()
    {
        var lf = new JsonLangFormat().Parse(
            "{ \"@code\": \"ko\", \"@name\": \"한국어\", \"menu.file\": \"파일\" }");
        Assert.Equal("ko", lf.Code);
        Assert.Equal("한국어", lf.Name);
        Assert.Equal("파일", lf.Strings["menu.file"]);
        Assert.False(lf.Strings.ContainsKey("@code"));   // 메타는 문자열 테이블에서 제외
    }

    [Fact]
    public void Json_skips_non_string_values()
    {
        var lf = new JsonLangFormat().Parse("{ \"a\": \"x\", \"b\": 3, \"c\": true }");
        Assert.Equal("x", lf.Strings["a"]);
        Assert.False(lf.Strings.ContainsKey("b"));
        Assert.False(lf.Strings.ContainsKey("c"));
    }

    [Fact]
    public void Properties_parses_meta_strings_comments_escapes()
    {
        string src = string.Join("\n",
            "# comment",
            "@code = ko",
            "@name = 한국어",
            "",
            "menu.file = 파일",
            "note = line1\\nline2");
        var lf = new PropertiesLangFormat().Parse(src);
        Assert.Equal("ko", lf.Code);
        Assert.Equal("한국어", lf.Name);
        Assert.Equal("파일", lf.Strings["menu.file"]);
        Assert.Equal("line1\nline2", lf.Strings["note"]);   // \n 해제
        Assert.False(lf.Strings.ContainsKey("# comment"));
    }

    [Fact]
    public void Properties_skips_malformed_lines()
    {
        var lf = new PropertiesLangFormat().Parse("garbage line no equals\nok = 1");
        Assert.Single(lf.Strings);
        Assert.Equal("1", lf.Strings["ok"]);
    }

    // 포맷 대등성 — 같은 논리 내용을 JSON/properties로 각각 표현하면 LangFile 결과가 동일(전환 무손실).
    [Fact]
    public void Json_and_properties_are_equivalent()
    {
        var j = new JsonLangFormat().Parse(
            "{ \"@code\": \"en\", \"menu.file\": \"File\", \"menu.exit\": \"Exit\" }");
        var p = new PropertiesLangFormat().Parse("@code = en\nmenu.file = File\nmenu.exit = Exit");
        Assert.Equal(j.Code, p.Code);
        Assert.Equal(j.Strings, p.Strings);   // 딕셔너리 동등 비교
    }

    [Fact]
    public void MergeStrings_user_overrides_install_per_key()
    {
        var install = new LangFile(new Dictionary<string, string>(),
            new Dictionary<string, string> { ["a"] = "A", ["b"] = "B" });
        var user = new LangFile(new Dictionary<string, string>(),
            new Dictionary<string, string> { ["b"] = "B2", ["c"] = "C" });
        var merged = LangFile.MergeStrings(new[] { install, user });   // 낮음→높음
        Assert.Equal("A", merged["a"]);    // 설치본 유지
        Assert.Equal("B2", merged["b"]);   // 사용자 오버라이드
        Assert.Equal("C", merged["c"]);    // 사용자 추가
    }

    [Fact]
    public void Active_format_is_json_by_default()
    {
        Assert.Equal("json", LangFormats.Active.Id);
        Assert.Equal("properties", LangFormats.ById("properties").Id);
    }
}
