using System.Collections.Generic;
using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

/// <summary>Localizer(i18n) 조회·폴백·포맷 규약 단위 테스트(맥/Win 공통).</summary>
public sealed class LocalizerTests
{
    private static Localizer Make() => new(
        "ko",
        new Dictionary<string, string> { ["menu.file"] = "파일", ["greet"] = "{0}님 환영" },
        new Dictionary<string, string> { ["menu.file"] = "File", ["only.en"] = "OnlyEnglish" });

    [Fact]
    public void Table_hit_returns_translation()
    {
        Assert.Equal("파일", Make().T("menu.file"));
    }

    [Fact]
    public void Falls_back_to_fallback_table()
    {
        Assert.Equal("OnlyEnglish", Make().T("only.en"));   // 현재 테이블에 없음 → en 폴백
    }

    [Fact]
    public void Missing_key_returns_key_itself()
    {
        Assert.Equal("no.such.key", Make().T("no.such.key"));
    }

    [Fact]
    public void Null_or_empty_key_returns_empty()
    {
        Assert.Equal(string.Empty, Make().T(""));
    }

    [Fact]
    public void Format_args_filled()
    {
        Assert.Equal("홍길동님 환영", Make().T("greet", "홍길동"));
    }

    [Fact]
    public void Format_mismatch_returns_raw()
    {
        // 자리표는 있는데 인자 부족 → 예외 삼키고 원문 반환(런타임 안전).
        Assert.Equal("{0}님 환영", Make().T("greet"));
    }

    [Fact]
    public void Empty_localizer_is_identity()
    {
        var loc = new Localizer("", new Dictionary<string, string>());
        Assert.Equal("any.key", loc.T("any.key"));
    }
}
