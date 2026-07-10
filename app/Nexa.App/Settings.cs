using System;
using System.Collections.Generic;

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

    /// <summary>경로 바 아래 "현재 경로 — N개 항목" 헤더 줄 표시 여부(기본 <c>false</c>=감춤).
    /// 표시(S) 메뉴 토글로 변경, 설정 UI 노출은 후속.</summary>
    public bool ShowPathHeader { get; set; }

    /// <summary>
    /// 파일 전송(복사/이동) <b>진행 창</b>을 완료 후 자동으로 닫을지(기본 <c>true</c> — 성공 시 <b>2초 카운트다운</b>
    /// 후 닫힘, 닫기 버튼에 "닫기 (2→1)" 표시). 실패/취소는 설정과 무관하게 열린 채 유지(결과 확인).
    /// 토글의 설정 UI 노출은 후속(DND-OW2).
    /// </summary>
    public bool AutoCloseTransferWindow { get; set; } = true;
}

/// <summary>
/// 컨텍스트 메뉴 커스텀 항목 사용자화(docs/38 §7) — 항목 추가/제거·순서·섹션 위치.
/// 설정 화면(메뉴 페이지)은 후속 구현, 스키마·적용 경로는 확정.
/// </summary>
internal sealed class MenuOptions
{
    /// <summary>제거(숨김)할 항목 Id 집합 — 레지스트리 Id 기준(paste-into/rename/delete-permanent/checksum …).</summary>
    public HashSet<string> DisabledItems { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>항목별 순서 재정의(미지정=DefaultOrder). 값이 작을수록 위.</summary>
    public Dictionary<string, int> OrderOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>커스텀 섹션 위치 — false=셸 항목 아래(기본) / true=셸 항목 위.</summary>
    public bool CustomSectionOnTop { get; set; }
}

/// <summary>일반 옵션 — 언어(i18n) 등. Culture: ""=시스템, "ko"/"en"(docs/40 언어 페이지).</summary>
internal sealed class GeneralOptions
{
    /// <summary>UI 언어 코드. "" = 시스템 UI 문화 추종, "ko"/"en" = 강제. 변경은 재시작 후 완전 반영.</summary>
    public string Culture { get; set; } = string.Empty;
}

/// <summary>테마 모드(docs/39). System=OS 설정 추종.</summary>
internal enum AppThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>
/// 테마 옵션(docs/39) — 라이트/다크 모드. 후속: 테마팩(토큰 색 오버라이드)·폰트·크기(밀도) 세부 설정
/// (설정 UI "모양" 페이지, docs/39 §5). 기본 <c>Light</c> — 라이트 팔레트 검증용(다크 팔레트 정비 후
/// 기본값 재결정, DR-2 참고).
/// </summary>
internal sealed class ThemeOptions
{
    public AppThemeMode Mode { get; set; } = AppThemeMode.Light;
}

/// <summary>
/// 글꼴 옵션(PREF-3, docs/40) — 영역별 6종 슬롯. Family는 쉼표(,)로 여러 개 지정 시 앞에서부터 폴백
/// (WinUI 합성 폰트 문자열 그대로 사용). 크기 px. 적용 배선 = <c>MainWindow.ApplyFonts</c>.
/// </summary>
internal sealed class FontOptions
{
    /// <summary>기본 글꼴 — 특별 지정 없는 곳(메뉴·설명·하단 정보/미리보기 창).</summary>
    public string BaseFamily { get; set; } = "Segoe UI";
    public double BaseSize { get; set; } = 12;

    /// <summary>콘솔(터미널) 글꼴 — 쉼표로 2개 이상 지정 가능("Cascadia Mono, Consolas").</summary>
    public string ConsoleFamily { get; set; } = "Consolas";
    public double ConsoleSize { get; set; } = 13;

    // 경로(브레드크럼)·탭 제목은 별도 슬롯 없이 기본 글꼴(Base)을 따른다(사용자 결정 2026-07-10).

    /// <summary>상태표시줄 글꼴.</summary>
    public string StatusFamily { get; set; } = "Segoe UI";
    public double StatusSize { get; set; } = 12;

    /// <summary>컨텍스트 메뉴 글꼴 — 앱이 그리는 플랫 메뉴(탭·빈 영역 우클릭). 셸(HMENU) 메뉴는 OS 글꼴.</summary>
    public string MenuFamily { get; set; } = "Segoe UI";
    public double MenuSize { get; set; } = 12;

    /// <summary>파일 목록 글꼴 — 좌/우 패널 목록 + 컬럼 헤더(글꼴/크기 공유).</summary>
    public string ListFamily { get; set; } = "Segoe UI";
    public double ListSize { get; set; } = 12;

    /// <summary>파일 목록에서 폴더 이름을 굵게(기본 true — 기존 표시 유지).</summary>
    public bool FolderBold { get; set; } = true;

    /// <summary>파일 헤더(컬럼 헤더) 꾸미기 — 글꼴/크기는 파일 목록과 동일, 두껍게/기울임만 지정.</summary>
    public bool HeaderBold { get; set; } = true;
    public bool HeaderItalic { get; set; }
}

/// <summary>
/// 도구 모음 옵션(docs/44) — 그룹/그룹 내 항목의 표시 순서 재정의. 비면 레지스트리 기본 순서.
/// 순서 목록에 없는 id는 기본 순서를 유지하며 뒤에 붙는다(새 항목 추가에 안전).
/// </summary>
internal sealed class ToolbarOptions
{
    /// <summary>그룹 표시 순서(그룹 id 목록). 비면 기본.</summary>
    public List<string> GroupOrder { get; } = new();

    /// <summary>그룹별 항목 표시 순서(그룹 id → 항목 id 목록). 없으면 기본.</summary>
    public Dictionary<string, List<string>> ItemOrder { get; } = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>테마 옵션(라이트/다크 모드, docs/39).</summary>
    public static ThemeOptions Theme { get; } = new();

    /// <summary>컨텍스트 메뉴 사용자화(커스텀 항목 표시/순서/위치, docs/38 §7).</summary>
    public static MenuOptions Menu { get; } = new();

    /// <summary>일반 옵션(언어 등, docs/40).</summary>
    public static GeneralOptions General { get; } = new();

    /// <summary>글꼴 옵션(영역별 슬롯, PREF-3).</summary>
    public static FontOptions Fonts { get; } = new();

    /// <summary>도구 모음 옵션(그룹/항목 표시 순서, docs/44).</summary>
    public static ToolbarOptions Toolbar { get; } = new();

    // 후속: LoadFromJson(path) / SaveToJson(path) — System.Text.Json. 변경 시 저장·실행 시 로드.
    //  · 설정 시스템/단축키·명령 레지스트리 설계: docs/26 §5.
    //  · 창 위치/크기 복원 + 다중 모니터 보정(WindowPlacement, state.json): docs/28.
}
