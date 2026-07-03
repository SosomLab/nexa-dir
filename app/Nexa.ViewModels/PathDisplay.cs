namespace Nexa.ViewModels;

/// <summary>경로 → 표시 문자열 변환(UI 비종속 순수 로직). 맥/Windows 공통 단위 테스트 대상.</summary>
public static class PathDisplay
{
    /// <summary>
    /// 탭/헤더에 표시할 이름 = 폴더명. 루트/드라이브(예: <c>C:\</c>)처럼 폴더명이 비면 경로 자체를 쓴다.
    /// 끝의 구분자(<c>\</c>·<c>/</c>)는 무시한다.
    /// </summary>
    public static string TabTitle(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
