using System;
using System.Collections.Generic;
using System.IO;

namespace Nexa.ViewModels;

/// <summary>되돌릴 수 있는 파일 작업 1건(배치=1 트랜잭션) — B-13u([docs/33]). 실패는 예외로 전파(무결성 우선).</summary>
public interface IReversibleOp
{
    /// <summary>상태바 표기용 설명(예: "이동 3개", "이름 변경: a → b").</summary>
    string Description { get; }

    /// <summary>작업을 되돌린다. 일부 항목 실패 시 나머지는 최선 수행 후 집계 예외.</summary>
    void Undo();

    /// <summary>되돌린 작업을 다시 수행한다.</summary>
    void Redo();
}

/// <summary>
/// 파일 작업 <b>undo/redo 히스토리</b>(B-13u, 탐색기 Ctrl+Z/Y) — 스택 2개. 새 작업 push 시 redo는 무효화.
/// 세션 한정(영속 X). 연산 실행 중 예외가 나면 그 연산은 스택에서 제거된 채 전파된다
/// (부분 실패 상태의 재실행은 더 위험 — 호출자는 상태바 알림, docs/33 "무결성 우선").
/// </summary>
public sealed class OperationHistory
{
    private readonly List<IReversibleOp> _undo = new();
    private readonly List<IReversibleOp> _redo = new();
    private readonly int _capacity;

    public OperationHistory(int capacity = 100) => _capacity = Math.Max(1, capacity);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoDescription => CanUndo ? _undo[^1].Description : null;
    public string? RedoDescription => CanRedo ? _redo[^1].Description : null;

    /// <summary>완료된 작업 기록 — redo 스택은 비워진다(표준 undo 모델). 상한 초과 시 가장 오래된 것 제거.</summary>
    public void Push(IReversibleOp op)
    {
        _undo.Add(op);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveAt(0);
        }
        _redo.Clear();
    }

    /// <summary>마지막 작업 되돌리기. 없으면 null. 예외 시 해당 연산은 양쪽 스택에서 제외된 채 전파.</summary>
    public IReversibleOp? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }
        var op = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        op.Undo();          // throw → op 소실(재시도 불가) — 호출자 알림
        _redo.Add(op);
        return op;
    }

    /// <summary>마지막 되돌리기를 재수행. 없으면 null. 예외 규약은 <see cref="Undo"/>와 동일.</summary>
    public IReversibleOp? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }
        var op = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        op.Redo();
        _undo.Add(op);
        return op;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

/// <summary>이동 배치 — undo: dest→src 역이동 / redo: src→dest 재이동. 소실·충돌 항목은 건너뛰고 집계 예외.</summary>
public sealed class MoveBatchOp : IReversibleOp
{
    private readonly IReadOnlyList<(string Src, string Dest)> _pairs;

    public MoveBatchOp(IReadOnlyList<(string Src, string Dest)> pairs, string description)
    {
        _pairs = pairs;
        Description = description;
    }

    public string Description { get; }

    public void Undo() => MoveAll(from: p => p.Dest, to: p => p.Src);

    public void Redo() => MoveAll(from: p => p.Src, to: p => p.Dest);

    private void MoveAll(Func<(string Src, string Dest), string> from, Func<(string Src, string Dest), string> to)
    {
        int failed = 0;
        foreach (var pair in _pairs)
        {
            string src = from(pair);
            string dest = to(pair);
            try
            {
                if (!FileOps.Exists(src) || FileOps.Exists(dest))
                {
                    failed++;   // 원본 소실(외부 변경) 또는 대상 충돌 → 건너뜀(무결성 — 덮어쓰지 않음)
                    continue;
                }
                FileOps.MoveOnto(src, dest, overwrite: false);
            }
            catch (Exception)
            {
                failed++;
            }
        }
        if (failed > 0)
        {
            throw new IOException($"{failed}개 항목을 처리하지 못했습니다(소실/충돌).");
        }
    }
}

/// <summary>복사 배치 — undo: 사본 삭제(주입 액션 — 앱은 휴지통) / redo: 재복사. 소실·충돌은 건너뛰고 집계 예외.</summary>
public sealed class CopyBatchOp : IReversibleOp
{
    private readonly IReadOnlyList<(string Src, string Dest)> _pairs;
    private readonly Action<string> _deleteCopy;   // 사본 제거 방법(앱=휴지통 · 테스트=완전삭제) — Windows 전용 API 격리

    public CopyBatchOp(IReadOnlyList<(string Src, string Dest)> pairs, string description, Action<string> deleteCopy)
    {
        _pairs = pairs;
        Description = description;
        _deleteCopy = deleteCopy;
    }

    public string Description { get; }

    public void Undo()
    {
        int failed = 0;
        foreach (var (_, dest) in _pairs)
        {
            try
            {
                if (FileOps.Exists(dest))
                {
                    _deleteCopy(dest);
                }
            }
            catch (Exception)
            {
                failed++;
            }
        }
        if (failed > 0)
        {
            throw new IOException($"{failed}개 사본을 삭제하지 못했습니다.");
        }
    }

    public void Redo()
    {
        int failed = 0;
        foreach (var (src, dest) in _pairs)
        {
            try
            {
                if (!FileOps.Exists(src) || FileOps.Exists(dest))
                {
                    failed++;   // 원본 소실/대상 충돌 → 건너뜀
                    continue;
                }
                FileOps.CopyOnto(src, dest, overwrite: false);
            }
            catch (Exception)
            {
                failed++;
            }
        }
        if (failed > 0)
        {
            throw new IOException($"{failed}개 항목을 다시 복사하지 못했습니다(소실/충돌).");
        }
    }
}

/// <summary>이름 변경 — undo: new→old / redo: old→new. 소실·충돌은 예외.</summary>
public sealed class RenameOp : IReversibleOp
{
    private readonly string _oldPath;
    private readonly string _newPath;

    public RenameOp(string oldPath, string newPath, string description)
    {
        _oldPath = oldPath;
        _newPath = newPath;
        Description = description;
    }

    public string Description { get; }

    public void Undo() => Rename(_newPath, _oldPath);

    public void Redo() => Rename(_oldPath, _newPath);

    private static void Rename(string from, string to)
    {
        if (!FileOps.Exists(from))
        {
            throw new IOException($"원본이 없습니다(외부 변경?): {FileOps.LeafName(from)}");
        }
        if (FileOps.Exists(to))
        {
            throw new IOException($"같은 이름이 이미 있습니다: {FileOps.LeafName(to)}");
        }
        FileOps.MoveOnto(from, to, overwrite: false);
    }
}

/// <summary>새로 만들기(폴더/파일/바로가기) — undo: 생성물 삭제(주입 — 앱=휴지통) / redo: 재생성(주입 델리게이트).</summary>
public sealed class CreateOp : IReversibleOp
{
    private readonly string _path;
    private readonly Action<string> _delete;
    private readonly Action _recreate;

    public CreateOp(string path, string description, Action<string> delete, Action recreate)
    {
        _path = path;
        Description = description;
        _delete = delete;
        _recreate = recreate;
    }

    public string Description { get; }

    public void Undo()
    {
        if (FileOps.Exists(_path))
        {
            _delete(_path);
        }
    }

    public void Redo()
    {
        if (!FileOps.Exists(_path))
        {
            _recreate();
        }
    }
}
