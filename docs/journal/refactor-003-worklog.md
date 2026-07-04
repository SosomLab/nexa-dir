# refactor/003-audit · 진행 로그 (시간순 · 6하원칙)

> **이 문서는 `refactor/003-audit` 브랜치에서 일어난 변화만** 시간순(YYYY-MM-DD HH:MM:SS, KST)으로 기록한다.
> 진행마다 **맨 끝에 append**(새 커밋마다 항목). 시각은 커밋 committer date 기준.
> 이전 라운드: [refactor-002-worklog.md](refactor-002-worklog.md) (통합 감사 → 트랙 A 성능 P1/P2/P3/P6 + 트랙 B 구조 PanelView·ViewModels + 문서·QA, main 병합 `1d9d312`).

## 브랜치 개요

- **브랜치**: `refactor/003-audit` (일련번호식 — 다음 라운드는 `refactor/004-…`)
- **분기 기준**: `1d9d312` (main, 2차 감사 병합 직후)
- **목적/요청**: 사용자 — "3차 검증용 브랜치에서 ①설계 방향 부합·구현/준비 충분성 ②성능 보장 충분성 ③배드스멜 제거·효율 구조 리팩터링 ④파일탐색기 FR·NFR 충분성 점검 → **할 일 목록화**. 구현은 지시하는 것만 진행."
- **선택된 진행 방향**: 4축 병렬 감사(E1) 완료 → **백로그 확정 후 사용자 지시 대기**(트랙 A~E). 구현 미착수.

## 진행 ↔ 커밋 계층 매핑 (요약)

```
refactor/003-audit  (분기: 1d9d312)
├─ E1 4축 병렬 감사 ............... 7997efc
│    └ 산출: 20260703_200457_refactor-003-audit.md · 백로그 docs/TODO.md(1f20738) · BUG-001 해결(8d3d363)
├─ E2 B-6 인라인 이름변경(1차) ..... (이 커밋)
└─ E3~ 사용자 지시 항목 진행 ...... 대기
```

## 3차 백로그 요약 (실행 목록·범위 산정: [../TODO.md](../TODO.md) · 근거: [감사 리포트](20260703_200457_refactor-003-audit.md) §★)

- **트랙 A 성능·메모리 보증**: A-1 `_cache` 상한(O(방문행수) 열화 제거)·A-2 다중탭 캡·A-3 arena 회수·A-4 유휴 트림·A-5 아이콘 큐 처리율·A-6 스크롤/Count·A-7 실측
- **트랙 B 핵심 기능(M1 P0)**: B-1 nexa-ops(파일작업+에러/취소/진행률/Undo/휴지통/충돌)·B-2 컨텍스트메뉴·B-3 클립보드·B-4 DnD+러버밴드·B-5 교차선택 완성·B-6 이름변경·B-7 사이드바
- **트랙 C 설계 계약**: C-1 에러 모델·C-2 VFS Provider·C-3 watcher·C-4 docs/01 현행화
- **트랙 D 인프라·영속·접근성**: D-1 설정 영속화+세션복원·D-2 i18n·D-3 접근성·D-4 NFR 재보정
- **트랙 E 구조·퀵윈**: E-1 죽은코드·E-2 순수추출·E-3 PanelControl·E-4 명령레지스트리·E-5 csbindgen·E-6 에러코드·E-7 DTO분리/Build.props·E-8 NavButtons

---

## E1 · 2026-07-03 · 4축 병렬 감사(설계·성능·구조·FR/NFR) → `(이 커밋)`

- **누가/왜**: 사용자 요청 — 3차 검증용 브랜치에서 4축 점검 후 할 일 목록화(구현은 지시 시).
- **어떻게**: `general-purpose` 4축 병렬(축1 설계부합·준비 / 축2 성능보장 / 축3 배드스멜·구조 / 축4 파일탐색기 FR·NFR) → 심각도·트랙별 통합.
- **핵심 결론**: 설계 훼손 없음·성능 토대 우수. 그러나 **①파일 조작 계층 전무(뷰어 상태, M1 P0)** **②성능이 "실보장" 아님(`_cache` 무경계 → 선택/포커스/토글 O(방문행수)·메모리 무캡)** **③설계 계약 공백(에러/Provider/watcher, M2+ 관문)** **④인프라 부채(설정 인메모리·i18n 0·접근성 0)**. 트랙 A~E 백로그 확정.
- **산출**: [20260703_200457_refactor-003-audit.md](20260703_200457_refactor-003-audit.md).

## E2 · 2026-07-03 · 트랙 B-6 — 인라인 이름 변경(1차, 선택 후 재클릭/F2) → `(이 커밋)`

- **왜/요청**: 사용자 — "클릭하면 선택, 선택된 상태에서 한 번 더 클릭하면 이름 변경. Enter=확정·ESC=취소·영역 밖 클릭=확정. 적당한 시간 간격." (감사 백로그 B-6, P2)
- **무엇 · 파일**:
  - [DirItem](../../app/Nexa.App/NativeInterop.cs): `IsRenaming`/`EditName` + `NameVisibility`/`EditVisibility`(편집 시 이름 TextBlock↔TextBox 전환).
  - [MainWindow.xaml](../../app/Nexa.App/MainWindow.xaml): 이름 셀에 편집 TextBox 오버레이(좌/우) + **컴팩트 편집기 스타일 `RenameBox`**(기본 TextBox의 MinHeight 32·헤더·지우기(×) 버튼이 행을 깨서 → 최소 템플릿: 테두리+ContentElement만, 행 높이 유지).
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): **트리거** — 직전 plain 클릭으로 단독 선택된 같은 항목을 **시스템 더블클릭 시간(`GetDoubleClickTime`) 이후** 재클릭 시 편집(빠른 재클릭=더블클릭=진입과 구분, 상한 없음=Explorer식). **F2**(캐럿 항목)도 지원. `Begin/Commit/CancelRename`·`OnRenameKeyDown`(Enter/Esc)·`OnRenameLostFocus`(영역 밖=커밋). 편집 시작 시 **확장자 제외 이름부 선택**. 검증(사용 불가 문자·중복·IO 실패→상태바 격리). 커밋 = `System.IO` `File/Directory.Move` 후 **폴더 재로드 + 새 경로 재선택**, 펼침 경로 접두사 갱신.
  - [NexaFileGrid](../../app/Nexa.Controls/NexaFileGrid.xaml.cs): `RowElement(index)` 노출(F2 편집 포커스용).
- **한계(후속·정석은 nexa-ops B-1)**: **Undo/휴지통 없음 — 즉시·불가역** · 배치 리네임(docs/25) 별도 · 예약이름/길이 미검증 · 셸 알림(SHChangeNotify) 없음 · watcher 미반영(C-3) · 커밋 시 폴더 전체 재로드(코어 rename API 인플레이스가 이상적) · 편집기 UIA 라벨 없음(D-3). 인라인 편집 컨트롤 승격은 수요 2곳+ 또는 ops 재배선 시.
- **검증**: 로컬 `dotnet build`(app x64) green(경고 0/오류 0). 실기 QA(선택-재클릭·Enter/Esc/영역밖·더블클릭 진입 구분·F2·검증) 사용자 확인. 레이아웃 깨짐 → 컴팩트 스타일로 해소.

## E3 · 2026-07-03 · B-6 후속 — 인라인 편집기 배경·크기 다듬기 → `(이 커밋)`

- **왜/요청**: 사용자 — "이름 변경 컨트롤 배경을 그리드 배경과 일치시켜 **border만** 보이게, 높이는 행보다 **위 1px·아래 1px 작게**." 좌/우 양쪽 패널 공통.
- **무엇 · 파일** [MainWindow.xaml](../../app/Nexa.App/MainWindow.xaml):
  - `RenameBox` 스타일 `Background` `#FF2B2B2B` → **`Transparent`**: 각 패널 그리드/행 배경(좌 파랑·우 빨강 틴트)이 그대로 비쳐 편집 영역은 **테두리로만** 구분. 스타일 공유라 **한 곳 수정 = 양쪽 패널 동시 적용**.
  - 편집 TextBox(좌·우 2곳): **`Margin="0,-2,0,-2"`**(명시적 `Height` 없음). 편집 폰트(12px) 자연 높이(~18px, 정상 텍스트보다 작음)로 잡히고 음수 마진으로 점유를 더 줄여(~14px) **행을 절대 넘지 않음**.
- **행이 늘어난 원인(실기 스크린샷 검토)**: 중간 시도 `Height="20"`(강제)가 정상 이름 셀보다 커서 행이 늘고 화면 스트레치 → **`Height` 강제를 제거**해 자연 크기로 복귀(정확히는 B-6 최초 구성 + 배경 투명). 음수 마진만으로 WinUI 점유 축소가 보장되지 않는 케이스가 있어 `Height` 미지정이 안전.
- **왜 커스텀 템플릿**: 일반 TextBox의 `MinHeight 32`·헤더·×버튼이 행을 깨는 문제는 `RenameBox` 템플릿(테두리+콘텐츠만)으로 제거됨. 남은 건 배경 투명 + 자연 높이.
- **폰트 색**: `RenameBox` `Foreground` `White` → **`{ThemeResource TextFillColorPrimaryBrush}`**(테마 기본 전경색). 정상 이름 TextBlock이 색 미지정=테마 기본이라, 편집 시에도 **원래 이름 색 유지**(라이트 테마에서 White로 뜨던 문제 해소).
- **검증**: WinUI 맥 빌드 불가 → **실기(Windows) 확인** 필요. 투명 배경이라 편집 중 선택 하이라이트가 테두리 안에 비침(의도). 편집 행 높이 = 정상 행 높이 목표.

## 트랙 B-7~B-13 · 파일 조작 상호작용 (사용자 지정, 순차 구현) — 개요

- **요청**: ①더블클릭 기본연결 실행 ②우클릭 컨텍스트 메뉴 ③드래그앤드롭 패널 내 폴더 이동(+자동 스크롤) ④좌우 패널 간 드래그앤드롭 ⑤드래그 중 탭 2초 hover→전환 후 드롭 ⑥복사/잘라내기/삭제(완전삭제). "기능 단위로 순차 구현, 자리 비우니 묻지 말고 모두 진행."
- **아키텍처 메모(감사 C-1 연계)**: 파일 작업(복사/이동/삭제)의 정석은 코어 `nexa-ops`(백그라운드 I/O·취소·진행률·Undo)이나 아직 크레이트 없음. B-6 리네임 선례대로 **C# `System.IO`로 구현하되 단일 헬퍼 `FileOps`에 중앙화**(향후 nexa-ops 이관 시 seam 1곳). 삭제는 요청대로 **완전삭제**(휴지통 아님).
- **단위**: B-7 더블클릭 실행 · B-8 FileOps+클립보드 · B-9 컨텍스트 메뉴 · B-10 복사/잘라/붙여/삭제+단축키 · B-11 DnD 패널내 이동+자동스크롤 · B-12 DnD 좌우 패널 · B-13 DnD 탭 hover 전환.
- **검증**: WinUI 맥 빌드 불가 → 단위마다 **PR CI(app) green** + 실기 QA.

## B-7 · 2026-07-03 · 더블클릭 파일 실행(기본 연결 프로그램) → `(이 커밋)`

- **무엇 · 파일** [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): `OnRowDoubleTapped`가 파일이면 return하던 것을 **`ActivateItem(left,item)` 호출로 교체** — 폴더/링크=진입, 파일=`Launcher.LaunchFileAsync`(연결 프로그램). `ActivateItem`은 기존에 이미 존재(키보드 Alt+↓ 경로에서만 쓰이던 것)라 로직 재사용, 더블클릭에 배선만.
- **검증**: 빌드(CI). 실기 QA: 파일 더블클릭 실행·폴더 더블클릭 진입.

## B-8 · 2026-07-03 · 파일 작업 헬퍼 FileOps + 앱 클립보드 모델(맥 테스트) → `(이 커밋)`

- **왜**: B-9~B-13(컨텍스트 메뉴·클립보드·DnD)의 공용 기반. 복사/이동/삭제를 흩뿌리지 않고 한 곳에 중앙화(향후 nexa-ops 이관 seam).
- **무엇 · 파일**(순수 `System.IO` → **`Nexa.ViewModels`(net8.0)** 배치, 맥 테스트 가능):
  - [FileOps](../../app/Nexa.ViewModels/FileOps.cs): `CopyInto`(파일/폴더 재귀, 충돌 시 " (2)"…)·`MoveInto`(제자리=무동작, 폴더 자기/하위 이동 방지)·`DeletePermanent`(완전삭제, 폴더 재귀)·`UniqueDest`/`LeafName`.
  - [FileClipboard](../../app/Nexa.ViewModels/FileClipboard.cs): 앱 내부 클립보드(경로 목록 + cut/copy 모드). 붙여넣기 시 cut=이동·copy=복사. (OS 클립보드 연동은 후속.)
  - [FileOpsTests](../../app/Nexa.ViewModels.Tests/FileOpsTests.cs): 복사(파일/재귀/충돌)·이동(제자리/자기이동)·삭제 7 테스트.
- **검증**: `dotnet test`(net10 roll-forward) **32 통과**(FileOps 7 포함). UI 배선은 B-9/B-10. 앱 빌드는 PR CI.

## B-9 · 2026-07-03 · 우클릭 컨텍스트 메뉴(열기·잘라·복사·붙여넣기·삭제·이름) → `(이 커밋)`

- **무엇 · 파일**:
  - [MainWindow.xaml](../../app/Nexa.App/MainWindow.xaml): 행 StackPanel에 `ContextRequested="OnRowContextRequested"`(좌/우 2곳).
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): `OnRowContextRequested`가 **프로그램적 `MenuFlyout`** 구성(열기/실행·잘라내기·복사·붙여넣기(클립보드 있을 때만)·삭제(완전)·이름 바꾸기) 후 클릭 위치에 표시. `ContextTargets`(클릭 항목이 선택에 있으면 선택 전체, 아니면 단일 선택)·`CopySelection`/`CutSelection`(FileClipboard)·`PasteInto`(cut=이동/copy=복사, FileOps)·`DeleteSelection`(**ContentDialog 확인** 후 완전삭제)·`ReloadPanel`(작업 후 재로드, 펼침 유지)·`PathEq`.
- **동작**: 우클릭 시 대상 미선택이면 단일 선택 후 메뉴. 붙여넣기=현재 패널 폴더로. 삭제=확인 대화상자(휴지통 아님, 불가역) 후 실행. 이름 바꾸기=B-6 `BeginRename` 재사용.
- **검증**: 앱 빌드 PR CI. 실기 QA: 우클릭 메뉴·복사/붙여넣기·잘라내기/붙여넣기(이동)·삭제 확인창·이름 바꾸기.

## B-10 · 2026-07-03 · 복사/잘라/붙여/삭제 단축키(Ctrl+C/X/V, Del) → `(이 커밋)`

- **무엇 · 파일** [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs):
  - 작업 메서드를 `(left, IReadOnlyList<string> targets)` 형태로 리팩터: `CopyPaths`/`CutPaths`/`DeletePaths` — 컨텍스트 메뉴(대상=`ContextTargets`)와 키보드(대상=`KeyboardTargets`)가 공유.
  - `KeyboardTargets(left)`: 현재 선택 있으면 선택 전체, 없으면 캐럿 항목.
  - `OnGridKeyDown`에 **Ctrl+C/X/V**(복사/잘라내기/붙여넣기)·**Delete**(완전삭제 확인) 추가 — 활성 패널 기준.
- **검증**: 앱 빌드 PR CI. 실기 QA: Ctrl+C→Ctrl+V(복사)·Ctrl+X→Ctrl+V(이동)·Del(확인창).

## B-11 · 2026-07-03 · 드래그앤드롭 폴더 이동 + 가장자리 자동 스크롤 → `(이 커밋)`

- **무엇 · 파일**:
  - [NexaFileGrid.xaml](../../app/Nexa.Controls/NexaFileGrid.xaml)/[.cs](../../app/Nexa.Controls/NexaFileGrid.xaml.cs): 본문 ScrollViewer `AllowDrop` + `DragOver`/`DragLeave`/`Drop` → **드래그 중 위/아래 가장자리(32px) 근처면 `DispatcherTimer`(50ms)로 반복 스크롤**(가장자리에 머무는 동안 계속). 드롭/이탈 시 정지.
  - [MainWindow.xaml](../../app/Nexa.App/MainWindow.xaml): 행 StackPanel에 `CanDrag`+`DragStarting`+`AllowDrop`+`DragOver`+`Drop`(좌/우 2곳).
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): `OnRowDragStarting`(대상=선택 또는 드래그 항목, 앱 내부 `_dragPaths`)·`OnRowDragOver`(폴더면 이동 수락+캡션, 자기 자신 제외)·`OnRowDrop`(폴더로 이동)·`MovePathsInto`(제자리 제외, 원본+대상 패널 재로드 — 좌우 겸용).
- **동작**: 파일/폴더를 다른 폴더 행 위로 드래그→드롭 = 그 폴더로 이동. 드래그 중 목록 가장자리에서 자동 스크롤. 대상이 다른 패널 폴더면 좌우 모두 반영(B-12 토대).
- **검증**: 앱 빌드 PR CI. 실기 QA: 드래그 이동·자동 스크롤(위/아래)·자기 폴더 드롭 방지·다중 선택 드래그.

## B-12 · 2026-07-03 · 좌우 패널 간 드래그앤드롭(배경 드롭=현재 폴더) → `(이 커밋)`

- **왜**: B-11에서 폴더 행 드롭은 이미 좌우 겸용(대상 패널 자동 판정). 남은 것은 **패널 빈 영역/파일 행에 드롭 = 그 패널의 현재 폴더로 이동**(다른 패널로 옮기는 가장 흔한 케이스).
- **무엇 · 파일**:
  - [NexaFileGrid.xaml.cs](../../app/Nexa.Controls/NexaFileGrid.xaml.cs): 본문 `DragOver`가 이동 수락(`AcceptedOperation=Move`) + 드롭 시 `BodyDropped` 이벤트(행이 소비 안 한 드롭). 도메인 비종속.
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): `OnRowDrop`이 **폴더 드롭만 소비**(파일/빈영역은 본문으로 버블). ctor에서 `DirGrid/DirGrid2.BodyDropped` 구독 → `OnPanelBackgroundDrop(destLeft)`가 그 패널 현재 폴더로 `MovePathsInto`.
- **동작**: 좌 패널 파일을 우 패널(빈 영역/현재 폴더)로 드래그→드롭 = 우 패널 폴더로 이동, 양쪽 재로드. 폴더 위 드롭이면 그 폴더로.
- **검증**: 앱 빌드 PR CI. 실기 QA: 좌→우 이동·우→좌 이동·폴더행 vs 빈영역 구분.

## B-13 · 2026-07-03 · 드래그 중 탭 2초 hover → 전환 후 드롭 → `(이 커밋)`

- **무엇 · 파일**:
  - [MainWindow.xaml](../../app/Nexa.App/MainWindow.xaml): 탭 Border에 `AllowDrop`+`DragOver`+`DragLeave`+`Drop`(좌/우 탭 템플릿 2곳).
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): `_tabDwellTimer`(2초) — `OnTabDragOver`가 탭 진입 시 타이머 시작(다른 탭이면 재시작), Tick에서 `SwitchToTab`(그 탭 폴더 표시). `OnTabDragLeave`=취소. `OnTabDrop`=그 탭 폴더로 즉시 이동(2초 안 기다려도 됨).
- **동작**: 파일 드래그한 채 다른 탭 위에 2초 머물면 그 탭이 활성화(폴더 보임) → 계속 드래그해 그 폴더로 드롭 가능. 탭에 바로 드롭하면 그 탭 폴더로 이동.
- **검증**: 앱 빌드 PR CI. 실기 QA: 탭 2초 hover 전환·탭 직접 드롭·hover 중 이탈 취소.

## 트랙 B-7~B-13 완료 — 파일 조작 상호작용 요약

7개 기능 단위 순차 구현 완료(각 개별 커밋): 더블클릭 실행·FileOps(맥 테스트)·컨텍스트 메뉴·복사/잘라/붙여/삭제 단축키·DnD 패널 내 이동+자동스크롤·좌우 패널 DnD·탭 hover 전환. **파일 작업은 C# `FileOps`에 중앙화**(향후 nexa-ops 이관 seam, 감사 C-1). 삭제=완전삭제(확인창). **전 기능 Windows 실기 QA 필요**(맥 빌드 불가, 상호작용 다수). 후속: OS 클립보드 연동·진행률/취소·Undo(nexa-ops).

## B-14 · 2026-07-03 · 드래그 중 ESC 취소 → `(이 커밋)`

- **요청**: 사용자 — "드래그 중 ESC로 동작 취소."
- **원리**: WinUI `CanDrag` 드래그는 OS 드래그 루프가 관장 → **ESC가 네이티브로 드래그를 취소**(드롭 미발생, `DropResult=None`). 앱은 종료 시 상태만 정리하면 된다.
- **무엇 · 파일**:
  - [NexaFileGrid.xaml.cs](../../app/Nexa.Controls/NexaFileGrid.xaml.cs): `StopDragAutoScroll()` 공개(취소 시 자동 스크롤 강제 정지).
  - [MainWindow.xaml](../../app/Nexa.App/MainWindow.xaml): 행에 `DropCompleted="OnRowDropCompleted"`.
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs): `OnRowDropCompleted` — dwell 타이머·자동 스크롤 정지 + `_dragPaths` 정리. 결과가 `None`(ESC 취소 등)이면 이동 없이 "드래그 취소" 표시.
- **효과**: ESC로 드래그를 취소하면 파일 이동이 일어나지 않고 hover 전환 타이머·자동 스크롤도 즉시 멈춤(잔여 상태 없음).
- **검증**: 앱 빌드 PR CI. 실기 QA: 드래그 중 ESC → 이동 취소·상태바 표시·타이머 정지.

## (설계) · 2026-07-03 · 타입어헤드 찾기 설계 방향 → `(이 커밋)`

- **요청**: 사용자 — "탐색기의 키보드 starts-with 찾기를 도입하되, **단레벨(탐색기) vs 다레벨(이 앱)** 차이로 유용성이 달라지는 지점을 **구현 전 설계로** 정리." 개발보다 방향 우선.
- **산출**: [docs/32 타입어헤드 찾기 설계](../32-typeahead-find.md).
- **핵심 결론**: 다레벨의 혼란은 "타입어헤드" 자체가 아니라 **"전역 최적 매치 점프"** 의미론 때문. → **경량 이동(타입어헤드)** 과 **명시적 탐색(검색)** 을 분리. 타입어헤드는 **가시 스트림 위치상대 + wrap starts-with**(옵션 C, Windows TreeView 선례)로 예측 가능하게, 접힌 것 포함 전역 찾기는 **명시적 검색(M3)** 으로. 아키텍처: 코어 `find_next_prefix`(맥 테스트) + `TypeAheadBuffer`(맥 테스트) + 앱 배선.
- **미결(구현 착수 전 확정)**: Space 충돌(선택 토글 vs 문자)·IME/한글 조합·범위 설정화·타임아웃. **구현은 사용자 승인 후.**

## (설계 확정) · 2026-07-03 · 타입어헤드 결정 반영 + 휘발성 표시 컨트롤 → `(이 커밋)`

- **사용자 결정 반영**([docs/32](../32-typeahead-find.md) 갱신): ①Space 제외(선택 토글 유지) ②IME 확정문자 매칭 ③범위 **A/B/C 설정 선택, 기본 C** ④타임아웃 1000ms **설정화**(`ViewOptions.TypeAheadScope`/`TypeAheadTimeoutMs`).
- **휘발성 검색어 표시 영역 검토**: 상태바(먼 위치·메시지 충돌·전역)·인라인 바(reflow) 대비 **활성 패널 목록 위 플로팅 오버레이(HUD)** 채택 — 시선 근처·레이아웃 불변·자동소거 정합.
- **전용 컨트롤 신설 설계**: `Nexa.Controls.EphemeralOverlay`(도메인 비종속 휘발성 텍스트 HUD, `Show(text)`/`Clear()`/`Timeout`, 페이드·자동소거) — 타입어헤드 외 순간 피드백 재사용.
- **구현 단위 확정(5단계)**: 코어 `find_prefix`(A/B/C) → `TypeAheadBuffer` → `EphemeralOverlay` → 설정 → 앱 배선. **다음 지시 시 1부터 순차 구현.** 여전히 설계만, 코드 없음.

<!-- 진행마다 아래에 6하원칙 항목 append -->

## 세션 요약 · 2026-07-04 · 파일 조작 UX 심화 배치 (B-7~B-15h·버그·컬럼·설계)

> 상세·시간기록: [TASKS.md](../TASKS.md) · 버그: [BUGS.md](../BUGS.md) · QA: [QA-003-checklist.md](../QA-003-checklist.md).
> CI는 결제 문제로 중단 → 이번 배치는 **사용자 Windows PC 빌드**로 검증(코어/ViewModels는 맥 테스트).

### 구현(커밋)
- **버그**: BUG-003 파일 실행 ShellExecute(desktop.ini 오류) · BUG-004 비활성 패널 클릭 활성화 · BUG-005 새 탭 이름 공백 · BUG-002 이름변경 드래그/우클릭 오발동 · **B-18t 이름변경 타이밍**(더블클릭 실행 시 오발동 → 지연 트리거) · **B-20p 경로바 편집 높이**(QA통과) · 빈 영역 클릭=선택취소(B-17c).
- **기능**: B-7 더블클릭 실행 · B-8/B-9m 마우스 재정비+다중선택 드래그 · B-10c 컨텍스트 메뉴 폴더/파일/빈영역 분리+F5 · B-11r 휴지통 삭제(Del=휴지통/Shift+Del=완전) · **B-14dnd 디스크별 DnD**(같은=이동/다른=복사, **Ctrl=복사/Shift=이동 강제** — Alt는 OS 가로채여 표준 채택) · B-15h 폴더 hover 진입+전환시간 설정(Tab/FolderDwellMs) · **B-12w watcher 1차**(FolderWatcher 자동 갱신) · COL-1 확장자 컬럼(소문자).
- **DnD 세부**: RequestedOperation=Move|Copy(Ctrl 복사 금지아이콘 해소·키 라이브 인식) · 캡션 텍스트 숨김(폰트 크기 OS 제어 불가).

### 설계(문서)
- [docs/32](../32-typeahead-find.md) 타입어헤드(A/B/C 설정·EphemeralOverlay).
- [docs/33](../33-file-ops-dnd-design.md) DnD 정책·폴더 hover·watcher Pub/Sub·Undo/Redo·**클립보드 개념+OS 연동 옵션**·**커스텀 드래그 비주얼 성능 검토**(런타임 0·정적 한계).
- [docs/23 §4-1](../23-column-system.md) 컬럼 정렬 구현 계획(COL-2a 코어→2b ABI→2c UI→COL-3, 3상태·Alt 다중키).

### 미결/대기
- **DnD glyph(↗ Move) 숨김 여부**(IsGlyphVisible) — 사용자 결정 대기. 폰트 크기는 OS 제어 불가(인앱 오버레이만 대안).
- **실기 QA**(QA-003 체크리스트) 다수 미확인. B-13t 탭 hover·B-15h 폴더 hover·B-20p 경로바·COL-1a 소문자는 QA 통과.
- **미착수(설계됨)**: COL-2/3 정렬(코어 비교자) · B-13u Undo/Redo · SHELL 셸 통합 · CLIP OS 클립보드 연동.

## 세션 요약 · 2026-07-04 · 탐색기식 드래그 파리티 (DnD 마감)

> 상세·시간기록: [TASKS.md](../TASKS.md)(DND-*) · 설계/검토: [docs/33 SHELL-DND](../33-file-ops-dnd-design.md).
> "드래그를 윈도우 탐색기와 동일하게" 요청 → 관리형으로 가능한 부분까지 구현 후, **한계 항목은 셸/OLE 트랙 개선 대상으로 정리하고 이 세션에서 드래그는 일단락**.

### 구현(커밋)
- **DND-CAP(Phase 1)** `6e43010`: WinUI 기본 큰 글리프("↗ Move") 끄고(`IsGlyphVisible=false`), 연산+대상 폴더명을 시스템 폰트 **라이브 캡션**("…에 복사"/"…(으)로 이동")으로. `ApplyDragCaption`/`FolderLabel`, 행·탭·빈영역 적용. `NexaFileGrid.DropTargetName`(ArmWatcher에서 갱신).
- **DND-SELF + DND-CAP2** `740d2da`: ① **자기 폴더 드롭 규칙** — 항목이 이미 그 폴더면 **Move=금지(None)/Copy=복제 허용(…(2))**(`BackgroundDragOp`, 커서·캡션·드롭 일관). ② **폴더 위 캡션 덮어쓰기 버그** — 드래그 이벤트 버블링(행→본문)으로 본문 핸들러가 폴더명 캡션을 배경 캡션으로 덮던 문제 → 본문은 **`AcceptedOperation==None`(행 미수락)일 때만** 처리, 폴더 행은 호스트 캡션 유지·자동스크롤은 항상. `NexaFileGrid.BodyDragOperation` 콜백 추가.

### 검토/결정(문서, 구현 안 함)
- [docs/33 SHELL-DND](../33-file-ops-dnd-design.md): 셸 드래그 통합 **성능 검토** — 드래그 런타임 0(셸/GPU 합성)·시작/드롭 1회성(지연 StorageItems+캐시 아이콘). 진짜 급소는 드래그가 아니라 **셸 컨텍스트 메뉴**(서드파티 핸들러 인프로세스 로드→느림/크래시).

### Windows QA 결과 & 남은 개선 대상(🅿️ 보류)
- ✅ 캡션 뜸. ✅ 폴더로 실제 이동/복사 정상.
- 🔴 **DND-KEY**: 키만 눌러선 라이브 갱신 안 됨(마우스 이동 필요). WinUI `DragOver`가 포인터 이동 시만 발생 → 관리형 불가, **셸/OLE 필요**.
- 🔴 **DND-FONT**: 캡션 폰트가 파일목록보다 큼(= Windows 표준 드래그 크기, API 없음). 축소하려면 커스텀 비트맵/셸.
- **DND-STACK**: 개수 스택 아이콘 — (2a)커스텀 비트맵 권장/(2b)COM 비권장.
- **결론**: DND-KEY/FONT/STACK은 관리형 한계로 **양립 불가**(작은 폰트=정적 ↔ 라이브=마우스이동) → **셸/OLE 트랙 착수 시 일괄 해결**. 드래그 기능 자체는 마감.
- **재검증 대기(2차 수정)**: DND-CAP2(폴더명 캡션)·DND-SELF(자기폴더 Move 금지/Copy 복제) Windows QA.

## 세션 요약 · 2026-07-04 · 헤더 정렬(COL-2/3) 전 계층 + Alt 메뉴

> 상세·시간기록: [TASKS.md](../TASKS.md)(COL-2a~2d·COL-3) · 설계 [docs/23 §4-1](../23-column-system.md).
> "헤더 정렬 개발" 요청 → 코어 비교자→ABI→관리형→UI→다중열→표시까지 순차 구현. 코어/ABI는 맥 단위테스트, UI는 사용자 Windows 빌드 검증.

### 구현(커밋)
- **COL-2a** `03bb90a`: 코어 `nexa-tree` 정렬 비교자 — `SortKey`(Name/Ext/Size/Modified/Kind/None)+`SortSpec{keys, folders_first}`, `set_sort`(로드 폴더 재정렬+가시목록 재구성, 펼침 보존). 실제 필드 비교(크기/날짜=숫자), 폴더 Size=0 정규화. **맥 테스트 7(17 green)**.
- **Alt 메뉴** `4e42db2`: `NexaMenuBar`에서 Alt 단독 탭 `OpenMenu(0)` 제거 → **Alt+문자 단축키로만** 메뉴 열림(열려 있으면 Alt로 닫기만).
- **COL-2b/2c** `3f7e7ee`: ABI `nexa_tree_set_sort`(`NexaSortKey` 배열·folders_first, **ABI v5→v6**)+관리형 `TreeSetSort`/`VirtualTreeCollection.SetSort`. UI: `NexaGridColumn`→`HeaderCell`, 헤더 클릭 3상태 순환→`SortRequested`→호스트 매핑→`SetSort`. **인터롭 테스트 6 green**.
- **COL-2d(좌/우 독립)** `7a80077`: 정렬 상태를 공유 컬럼→**패널별 `HeaderCell` 래퍼**로 이관(좌/우 독립 ▲▼). `OnSortRequested(bool left,…)` 클릭 패널만 적용. `PanelView.SortKeys`(null=코어기본/빈=열거/그외=지정)로 폴더 이동·탭 전환 지속. 너비는 여전히 공유(리사이즈 동기).
- **COL-3(다중열)** `8fb7f9f`: **Shift+헤더**(이미 정렬된 컬럼 ≥1일 때만) — `ApplyMultiSort`(추가/방향순환/제거+순번당김), 아니면 단일 리셋. 코어 다중키 재사용. 트리거 **Alt→Shift**(사용자 확정).
- **정렬 표시** `fc0f4a5`: 화살표(▲▼)를 **컬럼명 앞**, 순번을 **컬럼명 뒤 원문자**(①②③, `CircledNumber`). 빈 배지 `Collapsed`로 간격 제거.
- **빌드 정정** `4634d1a`: `using Windows.System` 제거(`DispatcherQueuePriority` CS0104 모호성) → `IsShiftDown` 타입 완전 정규화. 사용자 Windows 빌드 오류 리포트 반영.

### 상태
- **코어/ABI(COL-2a·2b)**: 맥 단위테스트 green(코어 17·인터롭 6). **ABI v6** — Windows에서 코어 cdylib 재빌드 필요(VerifyAbi가 v5 dll 거부).
- **UI(COL-2c·2d·3·표시)**: Windows 빌드 오류 1건 수정 후 재빌드 요청 상태. **실기 QA 대기**: 헤더 클릭 정렬·화살표 앞/원문자 뒤·Shift 다중열·좌우 독립·폴더 이동 지속·리사이즈 동기.
- **미착수**: COL-4(컬럼 조정 모달: 표시/순서/너비). 패널별 정렬은 HeaderCell로 달성, per-panel ColumnLayout(§6-3) 전면 도입은 COL-4와 함께 후속.
