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

<!-- 진행마다 아래에 6하원칙 항목 append -->
