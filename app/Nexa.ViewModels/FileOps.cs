using System.IO;

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
