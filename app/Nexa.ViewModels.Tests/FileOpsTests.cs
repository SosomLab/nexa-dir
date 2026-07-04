using System.IO;
using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

/// <summary>FileOps 복사/이동/완전삭제·고유 이름 단위 테스트(임시 폴더, 맥/Windows 공통).</summary>
public sealed class FileOpsTests : IDisposable
{
    private readonly string _base;

    public FileOpsTests()
    {
        _base = Path.Combine(Path.GetTempPath(), $"nexa_fileops_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_base);
    }

    public void Dispose()
    {
        try { Directory.Delete(_base, recursive: true); } catch { /* best effort */ }
    }

    private string Dir(string name)
    {
        string p = Path.Combine(_base, name);
        Directory.CreateDirectory(p);
        return p;
    }

    private string File_(string dir, string name, string content = "x")
    {
        string p = Path.Combine(dir, name);
        System.IO.File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void CopyInto_file_places_copy_in_dest()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "a.txt", "hello");

        string result = FileOps.CopyInto(f, dst);

        Assert.Equal(Path.Combine(dst, "a.txt"), result);
        Assert.True(System.IO.File.Exists(result));
        Assert.True(System.IO.File.Exists(f));   // 원본 보존
        Assert.Equal("hello", System.IO.File.ReadAllText(result));
    }

    [Fact]
    public void CopyInto_directory_is_recursive()
    {
        string src = Dir("src");
        string sub = Path.Combine(src, "folder");
        Directory.CreateDirectory(Path.Combine(sub, "nested"));
        File_(sub, "x.txt");
        File_(Path.Combine(sub, "nested"), "y.txt");
        string dst = Dir("dst");

        string result = FileOps.CopyInto(sub, dst);

        Assert.Equal(Path.Combine(dst, "folder"), result);
        Assert.True(System.IO.File.Exists(Path.Combine(result, "x.txt")));
        Assert.True(System.IO.File.Exists(Path.Combine(result, "nested", "y.txt")));
        Assert.True(Directory.Exists(sub));   // 원본 보존
    }

    [Fact]
    public void CopyInto_name_collision_gets_suffix()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        File_(dst, "a.txt", "existing");   // 대상에 이미 존재
        string f = File_(src, "a.txt", "new");

        string result = FileOps.CopyInto(f, dst);

        Assert.Equal(Path.Combine(dst, "a (2).txt"), result);
        Assert.Equal("new", System.IO.File.ReadAllText(result));
        Assert.Equal("existing", System.IO.File.ReadAllText(Path.Combine(dst, "a.txt")));
    }

    [Fact]
    public void MoveInto_file_moves_and_removes_source()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "m.txt", "data");

        string result = FileOps.MoveInto(f, dst);

        Assert.Equal(Path.Combine(dst, "m.txt"), result);
        Assert.True(System.IO.File.Exists(result));
        Assert.False(System.IO.File.Exists(f));   // 원본 제거됨
    }

    [Fact]
    public void MoveInto_same_dir_is_noop()
    {
        string src = Dir("src");
        string f = File_(src, "s.txt");

        string result = FileOps.MoveInto(f, src);   // 제자리

        Assert.Equal(f, result);
        Assert.True(System.IO.File.Exists(f));
    }

    [Fact]
    public void MoveInto_folder_into_self_or_child_throws()
    {
        string folder = Dir("parent");
        string child = Path.Combine(folder, "child");
        Directory.CreateDirectory(child);

        Assert.Throws<IOException>(() => FileOps.MoveInto(folder, child));   // 하위로
        Assert.Throws<IOException>(() => FileOps.MoveInto(folder, folder));  // 자기 자신
    }

    [Fact]
    public void DeletePermanent_removes_file_and_directory()
    {
        string src = Dir("src");
        string f = File_(src, "d.txt");
        string sub = Path.Combine(src, "tree");
        Directory.CreateDirectory(sub);
        File_(sub, "inner.txt");

        FileOps.DeletePermanent(f);
        Assert.False(System.IO.File.Exists(f));

        FileOps.DeletePermanent(sub);
        Assert.False(Directory.Exists(sub));

        FileOps.DeletePermanent(Path.Combine(src, "missing"));   // 없어도 예외 없음
    }
}
