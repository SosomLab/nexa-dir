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
    public void Conflicts_detects_existing_name_in_dest()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "c.txt");
        Assert.False(FileOps.Conflicts(dst, f));   // 아직 없음
        File_(dst, "c.txt", "old");
        Assert.True(FileOps.Conflicts(dst, f));    // 같은 이름 존재
    }

    [Fact]
    public void CopyOnto_overwrite_replaces_existing_file()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "o.txt", "new");
        File_(dst, "o.txt", "old");
        string dest = FileOps.NaturalDest(dst, f);

        FileOps.CopyOnto(f, dest, overwrite: true);

        Assert.Equal("new", System.IO.File.ReadAllText(dest));   // 순번 안 붙고 덮어씀
        Assert.True(System.IO.File.Exists(f));                   // 복사라 원본 보존
    }

    [Fact]
    public void CopyOnto_without_overwrite_throws_on_conflict()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "o.txt", "new");
        File_(dst, "o.txt", "old");
        Assert.Throws<IOException>(() => FileOps.CopyOnto(f, FileOps.NaturalDest(dst, f), overwrite: false));
    }

    [Fact]
    public void MoveOnto_overwrite_replaces_and_removes_source()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "o.txt", "new");
        File_(dst, "o.txt", "old");
        string dest = FileOps.NaturalDest(dst, f);

        FileOps.MoveOnto(f, dest, overwrite: true);

        Assert.Equal("new", System.IO.File.ReadAllText(dest));   // 덮어씀
        Assert.False(System.IO.File.Exists(f));                  // 이동이라 원본 제거
    }

    [Fact]
    public void CopyOnto_overwrite_replaces_directory()
    {
        string src = Dir("src");
        string sub = Path.Combine(src, "d");
        Directory.CreateDirectory(sub);
        File_(sub, "new.txt", "n");
        string dst = Dir("dst");
        string old = Path.Combine(dst, "d");
        Directory.CreateDirectory(old);
        File_(old, "old.txt", "o");   // 기존 대상 폴더에 다른 내용

        FileOps.CopyOnto(sub, FileOps.NaturalDest(dst, sub), overwrite: true);

        Assert.True(System.IO.File.Exists(Path.Combine(old, "new.txt")));   // 새 내용
        Assert.False(System.IO.File.Exists(Path.Combine(old, "old.txt")));  // 기존은 대체됨
    }

    [Fact]
    public void SizeOf_sums_file_and_directory()
    {
        string src = Dir("src");
        File_(src, "a.txt", "12345");   // 5 bytes
        string sub = Path.Combine(src, "s");
        Directory.CreateDirectory(sub);
        File_(sub, "b.txt", "678");     // 3 bytes

        Assert.Equal(5, FileOps.SizeOf(Path.Combine(src, "a.txt")));
        Assert.Equal(8, FileOps.SizeOf(src));
        Assert.Equal(0, FileOps.SizeOf(Path.Combine(src, "missing")));
    }

    [Fact]
    public void CopyOntoWithProgress_reports_total_bytes_and_copies()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "big.bin", new string('x', 10000));

        long reported = 0;
        FileOps.CopyOntoWithProgress(f, FileOps.NaturalDest(dst, f), overwrite: false, b => reported += b);

        Assert.Equal(10000, reported);
        Assert.True(System.IO.File.Exists(Path.Combine(dst, "big.bin")));
        Assert.Equal(10000, new FileInfo(Path.Combine(dst, "big.bin")).Length);
    }

    [Fact]
    public void MoveOntoWithProgress_same_volume_reports_size_and_moves()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "m.bin", new string('y', 2048));

        long reported = 0;
        FileOps.MoveOntoWithProgress(f, FileOps.NaturalDest(dst, f), overwrite: false, b => reported += b);

        Assert.Equal(2048, reported);                                   // 같은 볼륨 → 전체 크기 1회 보고
        Assert.True(System.IO.File.Exists(Path.Combine(dst, "m.bin")));
        Assert.False(System.IO.File.Exists(f));                        // 원본 제거
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
