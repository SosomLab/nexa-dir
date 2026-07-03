using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

public sealed class PathDisplayTests
{
    [Theory]
    [InlineData(@"C:\Users\kiros33", "kiros33")]
    [InlineData(@"C:\Users\kiros33\", "kiros33")]        // 끝 구분자 무시
    [InlineData(@"C:\Users\kiros33\Videos", "Videos")]
    [InlineData("/home/user/docs", "docs")]              // 슬래시 구분자
    public void TabTitle_returns_folder_name(string path, string expected) =>
        Assert.Equal(expected, PathDisplay.TabTitle(path));

    [Theory]
    [InlineData(@"C:\", @"C:\")]                          // 드라이브 루트 → 경로 자체
    [InlineData(@"C:", "C:")]
    public void TabTitle_falls_back_to_path_when_no_folder_name(string path, string expected) =>
        Assert.Equal(expected, PathDisplay.TabTitle(path));
}
