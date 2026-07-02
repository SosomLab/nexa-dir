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

/// <summary>탭 더블클릭 시 동작(설정, 기본 = 탭 닫기). 나머지는 후속 구현.</summary>
internal enum TabDoubleClickAction
{
    None,      // 아무 동작 없음
    Close,     // 탭 닫기(기본)
    Favorite,  // 즐겨찾는 탭으로 등록(후속)
    PopupMenu, // 탭 관련 팝업 메뉴(후속)
}

/// <summary>탭 관련 옵션.</summary>
internal sealed class TabOptions
{
    /// <summary>탭을 더블클릭했을 때의 동작(기본 = <see cref="TabDoubleClickAction.Close"/>). 나중에 설정 UI에서 선택.</summary>
    public TabDoubleClickAction DoubleClick { get; set; } = TabDoubleClickAction.Close;
}

/// <summary>
/// 목록 표시(가시성) 옵션. 숨김 파일과 점(.) 파일을 <b>독립</b>으로 토글한다(동시 설정 가능).
/// 기본값은 Windows 탐색기와 동일 — 숨김 속성 파일은 감추고, 점(.) 파일은 보인다.
/// </summary>
internal sealed class ViewOptions
{
    /// <summary>Windows 숨김 속성(FILE_ATTRIBUTE_HIDDEN) 파일 표시 여부(기본 <c>false</c> = 감춤).</summary>
    public bool ShowHiddenFiles { get; set; } = false;

    /// <summary>이름이 '.'으로 시작하는 리눅스식 점 파일/폴더를 숨길지(기본 <c>false</c> = 표시).</summary>
    public bool HideDotFiles { get; set; } = false;
}

/// <summary>
/// 앱 전역 설정(인메모리 단일 인스턴스). 나중에 JSON 파일 로드/저장으로 확장한다
/// (설정 시스템 백로그, docs/19). 지금은 코드 기본값이 유일한 원천.
/// </summary>
internal static class AppSettings
{
    /// <summary>정렬 옵션(폴더 우선 등). 목록 정렬은 이 값을 참조한다.</summary>
    public static SortOptions Sort { get; } = new();

    /// <summary>탭 옵션(더블클릭 동작 등).</summary>
    public static TabOptions Tab { get; } = new();

    /// <summary>표시(가시성) 옵션(숨김 파일·점 파일). 목록 필터는 이 값을 참조한다.</summary>
    public static ViewOptions View { get; } = new();

    // 후속: LoadFromJson(path) / SaveToJson(path) — System.Text.Json. 변경 시 저장·실행 시 로드.
}
