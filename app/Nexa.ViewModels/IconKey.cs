namespace Nexa.ViewModels;

/// <summary>
/// 셸 아이콘 캐시 키 계산(UI 비종속 순수 로직, 감사 P6). 16px 목록 아이콘은 대부분 <b>종류(확장자)별</b>
/// 타입 아이콘이라 확장자로 공유 캐시할 수 있다. 단 앱마다 <b>고유 아이콘</b>을 갖는 확장자(exe/lnk/ico…)는
/// 확장자로 뭉치면 안 되므로 <b>경로 키</b>(파일별)를 쓴다. 맥/Windows 공통 단위 테스트 대상.
/// </summary>
public static class IconKey
{
    // 파일별 고유 아이콘 → 확장자 공유 금지(경로 키). 소문자 비교.
    private static readonly HashSet<string> PerFile = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".ico", ".cur", ".msi", ".scr", ".appref-ms",
    };

    private static readonly char[] Separators = { '\\', '/' };

    /// <summary>
    /// 캐시 키 반환: 폴더=<c>"dir"</c> · 확장자 없음=<c>"file"</c> · 일반 파일=소문자 확장자(<c>".txt"</c>) ·
    /// 파일별 고유 아이콘 확장자(exe 등)=소문자 <b>전체 경로</b>. 구분자(<c>\</c>·<c>/</c>)를 직접 처리
    /// (<c>Path.*</c>는 플랫폼별 구분자만 인식 — 리눅스 테스트에서 Windows 경로 오처리 방지).
    /// </summary>
    public static string For(bool isDir, string path)
    {
        if (isDir)
        {
            return "dir";
        }
        string ext = Extension(FileName(path));
        if (ext.Length == 0)
        {
            return "file";
        }
        return PerFile.Contains(ext) ? path.ToLowerInvariant() : ext;
    }

    private static string FileName(string path)
    {
        var trimmed = path.TrimEnd(Separators);
        int sep = trimmed.LastIndexOfAny(Separators);
        return sep >= 0 ? trimmed[(sep + 1)..] : trimmed;
    }

    /// <summary>파일명의 소문자 확장자(<c>".txt"</c>). 확장자 없음/선행 점만(<c>.gitignore</c>)/끝점은 빈 문자열.</summary>
    private static string Extension(string name)
    {
        int dot = name.LastIndexOf('.');
        if (dot <= 0 || dot == name.Length - 1)
        {
            return string.Empty;
        }
        return name[dot..].ToLowerInvariant();
    }
}
