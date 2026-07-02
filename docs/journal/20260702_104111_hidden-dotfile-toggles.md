# 작업 기록 · 숨김 파일 보기 · 점(.) 파일 숨기기 (F24)

> 표시(S) 메뉴에 독립 토글 2개(동시 설정). 숨김 판정 원천을 **C# vs Rust 코어**로 비교 후 코어 확정.

## 질문·결정
- 요청: "숨김 파일 보기"를 설정 목록에 + "`.`으로 시작하는 리눅스식 숨김"을 옆에 두어 동시 설정.
- 1차 답변: C# 초안 / 기본값 둘 다 OFF(탐색기와 동일).
- 사용자 추가 질문(코어 vs C# 차이·성능) → 분석 제시:
  - **핵심**: Windows `FindNextFile`이 속성을 열거 결과에 이미 포함. Rust `DirEntry::metadata()`는 Windows에서 **추가 syscall 없음**. 코어가 이미 크기·시각용으로 `metadata()`를 부르므로 `attrs` 노출 비용 0.
  - C# `File.GetAttributes` 판정 = **엔트리당 syscall 1회**(10만 노드=10만 회) + 코어가 버린 정보 되사오기.
  - 점(.) 파일은 이름만 보면 되어 양쪽 무료.
- **재결정: Rust 코어 확장**(성능·정확성·"핫패스=코어" DR-1 정합). 기본값 유지(둘 다 OFF).

## 구현
- 코어 vfs: `Entry.attrs: u32` 추가, `file_attrs()`(`#[cfg(windows)]` `MetadataExt::file_attributes()`, 비Win=0). `read_dir_entries`가 기존 `metadata()`에서 함께 추출.
- 코어 interop: `NexaEntry.attrs` 추가·`nexa_dir_next` 채움. **ABI v1→v2**(`nexa_abi_version`), 테스트 `abi_version_is_two`.
- 앱 Settings: `ViewOptions{ShowHiddenFiles=false, HideDotFiles=false}` + `AppSettings.View`.
- 앱 interop: `DirItem.Attrs/IsHidden(0x2)/IsDotFile`, `ReadDir(..., view)` 필터 `IsVisible`(둘 다 통과해야 표시). 최상위+인라인 확장 모두 동일 필터.
- 메뉴: `NexaMenuEntry`에 `IsCheckable/IsChecked/Click` 추가. `NexaMenuBar.CreateItem`이 체크형이면 체크 글리프 칸 렌더 + 탭 시 토글 후 `Click` raise.
- MainWindow: 표시(S) 메뉴에 엔트리 2개, `OnToggleShowHidden/OnToggleHideDotFiles` → `AppSettings.View` 갱신 → `ReloadBothPanels`(펼침 유지).
- 곁들여: 탭 제목 폰트 9→12(상단 메뉴와 동일).

## 검증
- 코어: `cargo test --workspace` → 9 tests green(로컬 확인).
- 앱: 로컬 `dotnet build app/Nexa.App -c Debug` → **Build succeeded 0 err**(실행 중 인스턴스 종료 후). WinUI라 맥 빌드 불가 → push 후 **CI(windows) app job green 확인 필수**.
- Windows 수동: 표시(S)→숨김 파일 보기 체크 시 숨김 속성 파일 등장 / 점(.) 파일 숨기기 체크 시 `.git` 등 사라짐 / 두 토글 독립.

## 후속
- System(0x4) 속성 · 설정 JSON 영속화 · 앱 시작 시 메뉴 체크 상태 초기 동기화(현재 기본 OFF=엔트리 기본값 일치라 불필요).
