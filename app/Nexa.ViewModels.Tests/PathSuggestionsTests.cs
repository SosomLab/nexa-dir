using System;
using System.Collections.Generic;
using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

public class PathSuggestionsTests
{
    private static IEnumerable<string> FakeDirs(string baseDir)
    {
        Assert.Equal(@"C:\Users\", baseDir);   // 베이스가 구분자 포함으로 잘렸는지 함께 검증
        yield return @"C:\Users\Documents";
        yield return @"C:\Users\Downloads";
        yield return @"C:\Users\Desktop";
        yield return @"C:\Users\Public";
    }

    [Fact]
    public void 구분자끝_전체목록()
    {
        var r = PathSuggestions.SuggestFolders(@"C:\Users\", FakeDirs);
        Assert.Equal(4, r.Count);
    }

    [Fact]
    public void 접두사_대소문자무시_필터()
    {
        var r = PathSuggestions.SuggestFolders(@"C:\Users\do", FakeDirs);
        Assert.Equal(new[] { @"C:\Users\Documents", @"C:\Users\Downloads" }, r);
    }

    [Fact]
    public void 슬래시_구분자도_동작()
    {
        var r = PathSuggestions.SuggestFolders("C:/Users/de",
            b => { Assert.Equal("C:/Users/", b); return new[] { "C:/Users/Desktop", "C:/Users/Documents" }; });
        Assert.Equal(new[] { "C:/Users/Desktop" }, r);
    }

    [Fact]
    public void 구분자없음_빈목록()
    {
        Assert.Empty(PathSuggestions.SuggestFolders("C:", FakeDirs));
        Assert.Empty(PathSuggestions.SuggestFolders("", FakeDirs));
        Assert.Empty(PathSuggestions.SuggestFolders(null, FakeDirs));
    }

    [Fact]
    public void 열거_실패시_빈목록()
    {
        var r = PathSuggestions.SuggestFolders(@"X:\없는폴더\a",
            _ => throw new System.IO.DirectoryNotFoundException());
        Assert.Empty(r);
    }

    [Fact]
    public void 최대개수_제한()
    {
        var r = PathSuggestions.SuggestFolders(@"C:\Users\", FakeDirs, max: 2);
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void 끝구분자_있는_열거결과도_이름비교()
    {
        var r = PathSuggestions.SuggestFolders(@"C:\Users\pu",
            _ => new[] { @"C:\Users\Public\" });   // 끝 구분자 포함 형태도 이름 추출 정상
        Assert.Single(r);
    }
}
