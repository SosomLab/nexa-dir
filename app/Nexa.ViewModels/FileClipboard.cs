namespace Nexa.ViewModels;

/// <summary>
/// 앱 내부 파일 클립보드 — 복사/잘라내기한 경로 목록과 모드(cut/copy)를 보관한다(UI 비종속).
/// 붙여넣기 시 <see cref="IsCut"/>이면 이동, 아니면 복사. (OS 클립보드 연동은 후속 — 지금은 앱 내부.)
/// </summary>
public static class FileClipboard
{
    /// <summary>복사/잘라내기 대기 경로(삽입 순서). 비었으면 붙여넣기 불가.</summary>
    public static IReadOnlyList<string> Paths { get; private set; } = System.Array.Empty<string>();

    /// <summary>잘라내기(이동) 모드 여부. false면 복사.</summary>
    public static bool IsCut { get; private set; }

    /// <summary>붙여넣을 내용이 있는가.</summary>
    public static bool HasContent => Paths.Count > 0;

    /// <summary>복사 모드로 설정(붙여넣기 = 복사).</summary>
    public static void SetCopy(IEnumerable<string> paths)
    {
        Paths = new List<string>(paths);
        IsCut = false;
    }

    /// <summary>잘라내기 모드로 설정(붙여넣기 = 이동, 붙여넣기 후 비움).</summary>
    public static void SetCut(IEnumerable<string> paths)
    {
        Paths = new List<string>(paths);
        IsCut = true;
    }

    /// <summary>비운다(잘라내기 붙여넣기 완료 후 등).</summary>
    public static void Clear()
    {
        Paths = System.Array.Empty<string>();
        IsCut = false;
    }
}
