using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

public sealed class IconKeyTests
{
    [Fact]
    public void Directory_shares_one_key() =>
        Assert.Equal("dir", IconKey.For(isDir: true, @"C:\Users\kiros33\Videos"));

    [Theory]
    [InlineData(@"C:\x\a.txt", ".txt")]
    [InlineData(@"C:\x\a.TXT", ".txt")]     // 소문자 정규화
    [InlineData(@"C:\x\report.PDF", ".pdf")]
    [InlineData("/home/u/a.md", ".md")]      // 슬래시 경로
    public void Files_share_key_by_extension(string path, string expected) =>
        Assert.Equal(expected, IconKey.For(isDir: false, path));

    [Theory]
    [InlineData(@"C:\x\Makefile")]           // 확장자 없음
    [InlineData(@"C:\x\.gitignore")]         // 선행 점만 → 확장자 아님
    [InlineData(@"C:\x\trailing.")]          // 끝 점
    public void Files_without_extension_use_generic_key(string path) =>
        Assert.Equal("file", IconKey.For(isDir: false, path));

    [Theory]
    [InlineData(@"C:\Apps\Foo.exe")]
    [InlineData(@"C:\Users\me\Desktop\Bar.lnk")]
    [InlineData(@"C:\x\icon.ICO")]
    public void Per_file_icon_extensions_key_by_full_path(string path) =>
        Assert.Equal(path.ToLowerInvariant(), IconKey.For(isDir: false, path));

    [Fact]
    public void Two_different_exes_do_not_collide()
    {
        var a = IconKey.For(false, @"C:\Apps\A.exe");
        var b = IconKey.For(false, @"C:\Apps\B.exe");
        Assert.NotEqual(a, b);   // 앱별 고유 아이콘 보존
    }

    [Fact]
    public void Two_txt_files_share_key()
    {
        var a = IconKey.For(false, @"C:\x\one.txt");
        var b = IconKey.For(false, @"C:\y\two.txt");
        Assert.Equal(a, b);      // 타입 아이콘 공유
    }
}
