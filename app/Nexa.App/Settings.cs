namespace Nexa.App;

/// <summary>
/// 파일 목록 정렬 옵션. 현재는 <see cref="AppSettings"/>의 인메모리 기본값으로 동작하며,
/// 나중에 설정(JSON)·설정 UI에서 로드/저장·토글할 수 있도록 한곳으로 모은다.
/// </summary>
internal sealed class SortOptions
{
    /// <summary>폴더를 파일보다 먼저 표시(기본값 <c>true</c>). 나중에 설정에서 선택.</summary>
    public bool FoldersFirst { get; set; } = true;

    // 후속(A5): SortKey(이름/크기/날짜/종류) · Descending(정렬 방향) · 다중 정렬 키.
}

/// <summary>
/// 앱 전역 설정(인메모리 단일 인스턴스). 나중에 JSON 파일 로드/저장으로 확장한다
/// (설정 시스템 백로그, docs/19). 지금은 코드 기본값이 유일한 원천.
/// </summary>
internal static class AppSettings
{
    /// <summary>정렬 옵션(폴더 우선 등). 목록 정렬은 이 값을 참조한다.</summary>
    public static SortOptions Sort { get; } = new();

    // 후속: LoadFromJson(path) / SaveToJson(path) — System.Text.Json. 변경 시 저장·실행 시 로드.
}
