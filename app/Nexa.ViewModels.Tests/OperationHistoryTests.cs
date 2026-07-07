using System.IO;
using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

/// <summary>OperationHistory 스택 규약 + Move/Copy/Rename/Create 연산 undo/redo 왕복(임시 폴더, 맥/Win 공통).
/// 사본/생성물 삭제는 주입(테스트=완전삭제 — 휴지통은 Windows 전용이라 앱에서 주입).</summary>
public sealed class OperationHistoryTests : IDisposable
{
    private readonly string _base;

    public OperationHistoryTests()
    {
        _base = Path.Combine(Path.GetTempPath(), $"nexa_history_{Guid.NewGuid():N}");
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

    private sealed class FakeOp : IReversibleOp
    {
        public int Undone;
        public int Redone;
        public string Description => "fake";
        public void Undo() => Undone++;
        public void Redo() => Redone++;
    }

    // ── 스택 규약 ────────────────────────────────────────────────

    [Fact]
    public void Push_then_undo_then_redo_round_trip()
    {
        var h = new OperationHistory();
        var op = new FakeOp();
        h.Push(op);
        Assert.True(h.CanUndo);
        Assert.False(h.CanRedo);

        Assert.Same(op, h.Undo());
        Assert.Equal(1, op.Undone);
        Assert.False(h.CanUndo);
        Assert.True(h.CanRedo);

        Assert.Same(op, h.Redo());
        Assert.Equal(1, op.Redone);
        Assert.True(h.CanUndo);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void Push_clears_redo_stack()
    {
        var h = new OperationHistory();
        h.Push(new FakeOp());
        h.Undo();
        Assert.True(h.CanRedo);
        h.Push(new FakeOp());   // 새 작업 → redo 무효화(표준 모델)
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void Undo_empty_returns_null()
    {
        var h = new OperationHistory();
        Assert.Null(h.Undo());
        Assert.Null(h.Redo());
    }

    [Fact]
    public void Capacity_drops_oldest()
    {
        var h = new OperationHistory(capacity: 2);
        var first = new FakeOp();
        h.Push(first);
        h.Push(new FakeOp());
        h.Push(new FakeOp());
        h.Undo();
        h.Undo();
        Assert.False(h.CanUndo);   // first는 상한으로 제거됨
        Assert.Equal(0, first.Undone);
    }

    [Fact]
    public void Failing_undo_drops_op_and_propagates()
    {
        var h = new OperationHistory();
        // 소실 상태의 rename → 예외
        h.Push(new RenameOp(Path.Combine(_base, "no.txt"), Path.Combine(_base, "gone.txt"), "이름 변경"));
        Assert.Throws<IOException>(() => h.Undo());
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);   // 실패한 연산은 소실(무결성 우선)
    }

    // ── 연산 왕복 ────────────────────────────────────────────────

    [Fact]
    public void MoveBatchOp_undo_moves_back_and_redo_moves_again()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "a.txt", "hello");
        string moved = Path.Combine(dst, "a.txt");
        File.Move(f, moved);   // 원 작업(이동)이 이미 수행된 상태를 기록

        var op = new MoveBatchOp(new[] { (f, moved) }, "이동 1개");
        op.Undo();
        Assert.True(File.Exists(f));
        Assert.False(File.Exists(moved));

        op.Redo();
        Assert.False(File.Exists(f));
        Assert.True(File.Exists(moved));
    }

    [Fact]
    public void MoveBatchOp_undo_skips_conflict_and_throws_summary()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "a.txt");
        string moved = Path.Combine(dst, "a.txt");
        File.Move(f, moved);
        File_(src, "a.txt", "새로 생긴 충돌");   // undo 목적지에 외부 변경으로 파일 생김

        var op = new MoveBatchOp(new[] { (f, moved) }, "이동 1개");
        Assert.Throws<IOException>(() => op.Undo());
        Assert.True(File.Exists(moved));   // 덮어쓰지 않음(무결성)
    }

    [Fact]
    public void CopyBatchOp_undo_deletes_copy_and_redo_recopies()
    {
        string src = Dir("src");
        string dst = Dir("dst");
        string f = File_(src, "a.txt", "hello");
        string copied = Path.Combine(dst, "a.txt");
        File.Copy(f, copied);

        var op = new CopyBatchOp(new[] { (f, copied) }, "복사 1개", FileOps.DeletePermanent);
        op.Undo();
        Assert.True(File.Exists(f));       // 원본 유지
        Assert.False(File.Exists(copied)); // 사본 제거

        op.Redo();
        Assert.True(File.Exists(copied));
        Assert.Equal("hello", File.ReadAllText(copied));
    }

    [Fact]
    public void RenameOp_round_trip()
    {
        string dir = Dir("d");
        string a = File_(dir, "a.txt", "v");
        string b = Path.Combine(dir, "b.txt");
        File.Move(a, b);   // 원 작업(이름 변경) 수행됨

        var op = new RenameOp(a, b, "이름 변경: a.txt → b.txt");
        op.Undo();
        Assert.True(File.Exists(a));
        op.Redo();
        Assert.True(File.Exists(b));
        Assert.False(File.Exists(a));
    }

    [Fact]
    public void CreateOp_undo_deletes_and_redo_recreates()
    {
        string dir = Dir("d");
        string created = Path.Combine(dir, "새 폴더");
        Directory.CreateDirectory(created);

        var op = new CreateOp(created, "새 폴더", FileOps.DeletePermanent, () => Directory.CreateDirectory(created));
        op.Undo();
        Assert.False(Directory.Exists(created));
        op.Redo();
        Assert.True(Directory.Exists(created));
    }
}
