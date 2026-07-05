using System;
using System.IO;
using System.Threading;

namespace Nexa.ViewModels;

/// <summary>
/// 파일 작업 헬퍼 — 복사/이동/완전삭제(UI 비종속 순수 <c>System.IO</c> 로직). 맥/Windows 공통 단위 테스트.
/// <para>정석은 코어 <c>nexa-ops</c>(백그라운드 저우선순위 I/O·취소·진행률·Undo, 감사 C-1)이나 아직
/// 크레이트가 없다. B-6 인라인 리네임 선례대로 지금은 <c>System.IO</c>로 구현하되 <b>이 클래스 한 곳에
/// 중앙화</b>해 향후 nexa-ops 이관 시 seam이 1곳이 되게 한다. 삭제는 요청대로 <b>완전삭제</b>(휴지통 아님).</para>
/// </summary>
public static class FileOps
{
    /// <summary>원본(파일/폴더)을 <paramref name="destDir"/> 안으로 복사한다. 이름 충돌 시 " (2)"… 부여. 반환: 최종 경로.</summary>
    public static string CopyInto(string sourcePath, string destDir)
    {
        bool isDir = Directory.Exists(sourcePath);
        string name = LeafName(sourcePath);
        string dest = UniqueDest(destDir, name, isDir);
        if (isDir)
        {
            CopyDirectory(sourcePath, dest);
        }
        else
        {
            File.Copy(sourcePath, dest);
        }
        return dest;
    }

    /// <summary>
    /// 원본(파일/폴더)을 <paramref name="destDir"/> 안으로 이동한다. 이름 충돌 시 " (2)"… 부여. 반환: 최종 경로.
    /// 이미 그 폴더 안(제자리)이면 원본 경로 그대로 반환(무동작). 자기 자신/하위로의 폴더 이동은 예외.
    /// </summary>
    public static string MoveInto(string sourcePath, string destDir)
    {
        string src = sourcePath.TrimEnd('\\', '/');
        string? parent = Path.GetDirectoryName(src);
        if (parent is not null && PathEquals(parent, destDir))
        {
            return sourcePath;   // 제자리 이동 = 무동작
        }
        bool isDir = Directory.Exists(src);
        if (isDir && IsSameOrSubPath(src, destDir))
        {
            throw new IOException("폴더를 자기 자신 또는 하위 폴더로 이동할 수 없습니다.");
        }
        string dest = UniqueDest(destDir, LeafName(src), isDir);
        if (isDir)
        {
            Directory.Move(src, dest);
        }
        else
        {
            File.Move(src, dest);
        }
        return dest;
    }

    /// <summary><paramref name="destDir"/> 안에서 원본이 놓일 자연스러운 대상 경로(destDir + 잎 이름). 충돌 판정·덮어쓰기용.</summary>
    public static string NaturalDest(string destDir, string sourcePath) =>
        Path.Combine(destDir, LeafName(sourcePath));

    /// <summary><paramref name="sourcePath"/>를 <paramref name="destDir"/>에 넣을 때 <b>이름 충돌</b>이 있는가(같은 이름 존재).</summary>
    public static bool Conflicts(string destDir, string sourcePath) => Exists(NaturalDest(destDir, sourcePath));

    /// <summary>원본(파일/폴더)을 정확히 <paramref name="destPath"/>로 <b>복사</b>한다(순번 부여 안 함).
    /// <paramref name="overwrite"/>면 기존 대상을 대체(폴더는 삭제 후 재귀 복사).</summary>
    public static void CopyOnto(string sourcePath, string destPath, bool overwrite)
    {
        if (Directory.Exists(sourcePath))
        {
            if (overwrite && Directory.Exists(destPath))
            {
                Directory.Delete(destPath, recursive: true);
            }
            CopyDirectory(sourcePath, destPath);
        }
        else
        {
            File.Copy(sourcePath, destPath, overwrite);
        }
    }

    /// <summary>원본(파일/폴더)을 정확히 <paramref name="destPath"/>로 <b>이동</b>한다(순번 부여 안 함).
    /// <paramref name="overwrite"/>면 기존 대상을 대체. 자기 자신/하위로의 폴더 이동은 예외.</summary>
    public static void MoveOnto(string sourcePath, string destPath, bool overwrite)
    {
        string src = sourcePath.TrimEnd('\\', '/');
        bool isDir = Directory.Exists(src);
        if (isDir && IsSameOrSubPath(src, destPath))
        {
            throw new IOException("폴더를 자기 자신 또는 하위 폴더로 이동할 수 없습니다.");
        }
        if (overwrite)
        {
            if (Directory.Exists(destPath))
            {
                Directory.Delete(destPath, recursive: true);
            }
            else if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
        }
        if (isDir)
        {
            Directory.Move(src, destPath);
        }
        else
        {
            File.Move(src, destPath);
        }
    }

    // ── 진행률 지원 전송(대용량 파일 진행바용, DND-OW2) ──────────────────
    // File.Copy/Move는 바이트 진행 콜백이 없어 대용량 단일 파일에서 진행이 안 보인다.
    // 스트림 청크 복사로 바이트를 보고한다. onBytes는 이번에 복사한 증분 바이트(누적은 호출자).

    private const int CopyBufferSize = 4 * 1024 * 1024;   // 4MB 청크

    /// <summary>파일/폴더의 총 바이트 크기(폴더는 재귀 합계). 접근 실패/없음은 0으로 격리.</summary>
    public static long SizeOf(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                long sum = 0;
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { sum += new FileInfo(f).Length; } catch { /* 개별 격리 */ }
                }
                return sum;
            }
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch { /* 격리 */ }
        return 0;
    }

    /// <summary>파일을 스트림으로 복사하며 <paramref name="onBytes"/>로 진행(증분 바이트)을 보고한다.</summary>
    public static void CopyFileWithProgress(string src, string dest, bool overwrite, Action<long>? onBytes, CancellationToken ct = default)
    {
        using var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(dest, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[CopyBufferSize];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            onBytes?.Invoke(read);
        }
    }

    /// <summary>원본(파일/폴더)을 정확히 <paramref name="destPath"/>로 <b>복사</b>하며 진행 보고. overwrite면 기존 대체.</summary>
    public static void CopyOntoWithProgress(string sourcePath, string destPath, bool overwrite, Action<long>? onBytes, CancellationToken ct = default)
    {
        if (Directory.Exists(sourcePath))
        {
            if (overwrite && Directory.Exists(destPath))
            {
                Directory.Delete(destPath, recursive: true);
            }
            CopyDirectoryWithProgress(sourcePath, destPath, onBytes, ct);
        }
        else
        {
            CopyFileWithProgress(sourcePath, destPath, overwrite, onBytes, ct);
        }
    }

    /// <summary>원본(파일/폴더)을 정확히 <paramref name="destPath"/>로 <b>이동</b>하며 진행 보고. 같은 볼륨=즉시 이동(전체 크기 1회 보고), 다른 볼륨=복사 후 원본 삭제.</summary>
    public static void MoveOntoWithProgress(string sourcePath, string destPath, bool overwrite, Action<long>? onBytes, CancellationToken ct = default)
    {
        string src = sourcePath.TrimEnd('\\', '/');
        bool isDir = Directory.Exists(src);
        if (isDir && IsSameOrSubPath(src, destPath))
        {
            throw new IOException("폴더를 자기 자신 또는 하위 폴더로 이동할 수 없습니다.");
        }
        if (overwrite)
        {
            if (Directory.Exists(destPath)) { Directory.Delete(destPath, recursive: true); }
            else if (File.Exists(destPath)) { File.Delete(destPath); }
        }
        if (SameVolume(src, destPath))
        {
            if (isDir) { Directory.Move(src, destPath); } else { File.Move(src, destPath); }
            onBytes?.Invoke(SizeOf(destPath));   // 메타데이터 이동(즉시) → 전체 크기 1회 보고
        }
        else
        {
            CopyOntoWithProgress(src, destPath, overwrite: true, onBytes, ct);   // 다른 볼륨 = 복사(+진행)
            if (isDir) { Directory.Delete(src, recursive: true); } else { File.Delete(src); }
        }
    }

    /// <summary>두 경로가 같은 볼륨(드라이브 루트)인가. 판단 실패 시 true(보수적).</summary>
    public static bool SameVolume(string a, string b)
    {
        try
        {
            string ra = Path.GetPathRoot(Path.GetFullPath(a.TrimEnd('\\', '/'))) ?? string.Empty;
            string rb = Path.GetPathRoot(Path.GetFullPath(b.TrimEnd('\\', '/'))) ?? string.Empty;
            return string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    private static void CopyDirectoryWithProgress(string src, string dest, Action<long>? onBytes, CancellationToken ct)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
        {
            ct.ThrowIfCancellationRequested();
            CopyFileWithProgress(f, Path.Combine(dest, Path.GetFileName(f)), overwrite: true, onBytes, ct);
        }
        foreach (var d in Directory.GetDirectories(src))
        {
            CopyDirectoryWithProgress(d, Path.Combine(dest, Path.GetFileName(d)), onBytes, ct);
        }
    }

    /// <summary>파일/폴더를 <b>완전 삭제</b>한다(휴지통 아님, 폴더는 재귀). 없으면 무동작.</summary>
    public static void DeletePermanent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>파일/폴더를 <b>휴지통으로</b> 보낸다(되돌리기 가능, 일반 삭제). 없으면 무동작. Windows 전용.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void DeleteToRecycleBin(string path)
    {
        if (Directory.Exists(path))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        else if (File.Exists(path))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
        {
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)));
        }
        foreach (var d in Directory.GetDirectories(src))
        {
            CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
        }
    }

    /// <summary>경로 끝 구분자를 제거한 잎(파일/폴더) 이름.</summary>
    public static string LeafName(string path) => Path.GetFileName(path.TrimEnd('\\', '/'));

    /// <summary><paramref name="destDir"/> 안에서 <paramref name="name"/>이 비면 그대로, 충돌하면 " (2)"… 부여한 경로.</summary>
    public static string UniqueDest(string destDir, string name, bool isDir)
    {
        string path = Path.Combine(destDir, name);
        if (!Exists(path))
        {
            return path;
        }
        // 폴더는 확장자 분리 안 함(예: "v1.2" 폴더). 파일은 확장자 유지하고 " (n)"를 이름부에 부여.
        string stem = isDir ? name : Path.GetFileNameWithoutExtension(name);
        string ext = isDir ? string.Empty : Path.GetExtension(name);
        for (int i = 2; ; i++)
        {
            string cand = Path.Combine(destDir, $"{stem} ({i}){ext}");
            if (!Exists(cand))
            {
                return cand;
            }
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool PathEquals(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), System.StringComparison.OrdinalIgnoreCase);

    /// <summary><paramref name="child"/>가 <paramref name="ancestor"/>와 같거나 그 하위인가(폴더 자기이동 방지).</summary>
    private static bool IsSameOrSubPath(string ancestor, string child)
    {
        string a = ancestor.TrimEnd('\\', '/');
        string c = child.TrimEnd('\\', '/');
        if (PathEquals(a, c))
        {
            return true;
        }
        return c.StartsWith(a + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase)
            || c.StartsWith(a + "/", System.StringComparison.OrdinalIgnoreCase);
    }
}
