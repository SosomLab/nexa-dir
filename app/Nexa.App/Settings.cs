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
/// 두 토글 모두 "보기(표시)" 개념 — 기본은 <b>표시</b>(체크 ON), 해제하면 감춘다.
/// </summary>
internal sealed class ViewOptions
{
    /// <summary>Windows 숨김 속성(FILE_ATTRIBUTE_HIDDEN) 파일 표시 여부(기본 <c>true</c> = 표시, 해제 시 감춤).</summary>
    public bool ShowHiddenFiles { get; set; } = true;

    /// <summary>이름이 '.'으로 시작하는 리눅스식 점 파일/폴더 표시 여부(기본 <c>true</c> = 표시, 해제 시 감춤).</summary>
    public bool ShowDotFiles { get; set; } = true;

    /// <summary>
    /// 상위 폴더로 이동 시 방금 떠난 폴더(네비 대상)를 화면 어디에 보일지 — 세로 정렬 비율
    /// (0=맨 위, 0.5=가운데, 1=맨 아래). 기본 <b>가운데</b>. 나중에 설정 UI에서 선택(TODO, docs/26 §8).
    /// </summary>
    public double UpNavTargetAlign { get; set; } = 0.5;

    /// <summary>그리드 상단 헤더 정보란(경로 — N개 항목) 표시 여부(기본 <c>true</c>). 설정 UI 토글 예정(TODO).</summary>
    public bool ShowHeaderInfo { get; set; } = true;

    /// <summary>드래그 중 탭 위에 머물러 그 탭으로 전환되기까지 시간(ms, 기본 2000). 설정 UI 예정(B-15h).</summary>
    public int TabDwellMs { get; set; } = 2000;

    /// <summary>드래그 중 폴더 위에 머물러 그 폴더로 진입(spring-load)되기까지 시간(ms, 기본 3000). 설정 UI 예정(B-15h).</summary>
    public int FolderDwellMs { get; set; } = 3000;

    /// <summary>
    /// 복사/잘라내기/붙여넣기를 <b>OS 클립보드</b>와 연동할지(기본 <c>false</c>=앱 내부 클립보드).
    /// <para>true면 셸 상호운용(탐색기에서 복사→우리 앱 붙여넣기, 그 반대)이 되고, 다른 앱이 텍스트 등을
    /// 복사하면 파일 붙여넣기가 무효화된다(탐색기와 동일). 구현은 후속(설계: docs/33), 지금은 옵션 자리만.</para>
    /// </summary>
    public bool UseSystemClipboard { get; set; }

    /// <summary>
    /// 타입어헤드 찾기 범위(docs/32): 0=GlobalFirst(A) · 1=CurrentLevel(B) · <b>2=VisibleStream(C, 기본)</b>.
    /// 설정 UI 예정(TODO). 코어 <c>nexa_tree_find_prefix</c>의 scope 코드와 동일.
    /// </summary>
    public uint TypeAheadScope { get; set; } = 2;

    /// <summary>타입어헤드 입력 버퍼 리셋·검색어 표시 소거 타임아웃(ms, 기본 1000). 설정 UI 예정.</summary>
    public long TypeAheadTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// 파일 전송(복사/이동) <b>진행 창</b>을 완료 후 자동으로 닫을지(기본 <c>false</c>=열린 채 유지 → 사용자가 닫음).
    /// 탐색기처럼 완료 후에도 결과를 보여준다. 자동 닫기 토글의 설정 UI 노출은 후속(DND-OW2).
    /// </summary>
    public bool AutoCloseTransferWindow { get; set; }
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
    //  · 설정 시스템/단축키·명령 레지스트리 설계: docs/26 §5.
    //  · 창 위치/크기 복원 + 다중 모니터 보정(WindowPlacement, state.json): docs/28.
}
