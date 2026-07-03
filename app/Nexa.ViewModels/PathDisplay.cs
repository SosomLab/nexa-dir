namespace Nexa.ViewModels;

/// <summary>경로 → 표시 문자열 변환(UI 비종속 순수 로직). 맥/Windows 공통 단위 테스트 대상.</summary>
public static class PathDisplay
{
    private static readonly char[] Separators = { '\\', '/' };

    /// <summary>
    /// 탭/헤더에 표시할 이름 = 폴더명. 루트/드라이브(예: <c>C:\</c>)처럼 폴더명이 비면 경로 자체를 쓴다.
    /// 끝의 구분자(<c>\</c>·<c>/</c>)는 무시한다.
    /// <para><b>구분자를 직접 처리</b>한다 — <c>Path.GetFileName</c>은 플랫폼별 구분자만 인식해(리눅스에선 <c>\</c>를
    /// 무시) Windows 경로를 크로스플랫폼 테스트/실행에서 잘못 처리한다. 여기선 항상 <c>\</c>·<c>/</c> 둘 다 인식.</para>
    /// </summary>
    public static string TabTitle(string path)
    {
        var trimmed = path.TrimEnd(Separators);
        int sep = trimmed.LastIndexOfAny(Separators);
        var name = sep >= 0 ? trimmed[(sep + 1)..] : trimmed;
        // 폴더명이 없거나 드라이브(예: C:)면 원 경로를 표시.
        return string.IsNullOrEmpty(name) || name.EndsWith(':') ? path : name;
    }
}
