# DEVLOG — 개발 진행 기록 (시간 역순)

> **전체 개발 진행을 시간순(최신이 위)** 으로 관찰하는 단일 기록. 세부 커밋은 [BRANCHES.md](BRANCHES.md)·git 로그.
> 짝 문서: 목적·기능·마일스톤 관점 = **[MILESTONES.md](MILESTONES.md)**.
>
> **기록 규약(2026-07-05~)**: 진행 기록은 **일자 단위**로만 만든다.
> - 하루의 상세는 `journal/YYYY-MM-DD.md`(파일 **내부는 시간 역순**, 최신이 위)에 적는다.
> - 이 DEVLOG에는 그날의 **요약 섹션을 맨 위에 추가**(역순 유지)하고 해당 일자 파일로 링크한다.
> - 과거의 세션별/시각별(`YYYYMMDD_HHMMSS_*`) 저널과 라운드 워크로그(`refactor-00N-worklog`·`bottom-panel-worklog`)는
>   **통폐합 이전 아카이브**로 남긴다(상세 근거용). 신규 기록은 위 규약을 따른다.

---

## 2026-07-08

- **테마 시스템 S1(`feat/theme-system`→main)**: 영역 구분용 임시 틴트 전면 제거 → **시맨틱 토큰 10종**(App.xaml ThemeDictionaries)·**라이트 팔레트 정비**·구성(O) 메뉴 **테마 시스템/라이트/다크**(기본 Light — DR-2 다크 기본은 다크 정비 후 재결정), 후속 세부 설정 UI(테마팩·폰트·밀도) 설계 = [docs/39](39-theme-system.md). + **패널 보기 토글을 표시(S) 메뉴로 이관**(상태바=상태 전용) + **파일 목록 가로 스크롤**(넘칠 때만·헤더 동기). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **앱 아이콘 제작·적용**: GDI+ 생성기 `scripts/make-app-icon.ps1`(재현 가능) — 다크 라운드 + 폴더 루트 트리 + 다중 선택 accent 행 + `</>`(개발자 지향) 컨셉. PNG 9종(16~1024)+멀티사이즈 ICO, exe 임베드(ApplicationIcon)+창 SetIcon. 파일명 `nexa-dir-*`(공용 브랜드와 구분). 저널 **⏱ 시각 표기 규약** 도입(git 커밋 시각 기준, CLAUDE.md §6). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **breadcrumb 긴 경로 개선**: 폭 초과 시 끝(최근 폴더)으로 스크롤 유지(오른쪽 정렬 효과) — 경로 변경·리사이즈 공통. 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **탭 UX 일괄(TAB-DND·TAB-MENU, 9커밋)**: **탭 드래그 재정렬/패널 간 이동·Ctrl 복제**(탭 영역 한정·XAML/OLE 공용 계획·**삽입 위치 하이라이트·바 빈 영역=맨 끝**) · **탭 우클릭 메뉴**(새 탭/닫기/모두 닫기[잠금 보존]/잠금·고정 — 우클릭 시 탭 활성화) · **잠금=열쇠(오른쪽 끝)·고정=핀(이름 앞)+핀 그룹 정렬·세션 영속** · 긴 제목 말줄임(…) 복원 · **경로·항목 수 헤더 토글**(기본 감춤, 표시 메뉴). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **러버밴드(마퀴) 다중 선택(B-4 부분)**: 미선택 행/빈 공간 드래그=밴드 선택(선택 행=DnD 유지), 행 히트영역=컬럼 총폭(크기 뒤=배경), 4px 임계·가상화 안전 인덱스 범위·자동 스크롤. 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **B-13u Undo/Redo 마감 — 실기 QA 통과**(삭제 복원·복사·이동·새 폴더 전부 Ctrl+Z/Y 확인): **셸 메뉴 "삭제" 가로채기**(`verbInterceptor` — GCS_VERBW canonical verb 조회, "delete"→`DeletePaths` 라우팅으로 undo 기록 통합; 셸 직접 수행이라 미기록이던 버그 수정) + **빈 영역 메뉴에 실행 취소/다시 실행 항목**(마지막 작업 설명·Ctrl+Z/Y 표기·비활성 처리). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).

## 2026-07-07

- **B-13u Undo/Redo S1+S2 — 탐색기식 Ctrl+Z/Y 완성**: `Nexa.ViewModels.OperationHistory`(스택 2개·redo 무효화·실패=소실+알림) + `MoveBatchOp`/`CopyBatchOp`/`RenameOp`/`CreateOp`(삭제 주입으로 Windows API 격리, **xUnit 10건·총 67 green**) + **S2 `DeleteBatchOp`/`RecycleBin.cs`**(휴지통 셸 폴더 열거→원래 경로 매칭→`undelete` 동사 — 삭제 복원). 기록 배선 = `TransferPathsInto` 실수행 쌍(배치=1 트랜잭션)·이름변경·새로만들기 3종·휴지통 삭제. Ctrl+Z/Ctrl+Y(+Ctrl+Shift+Z), 상태바 표기, 완료 후 재로드. 잔여=nexa-ops 이관·다중 삭제본 시각 비교. 상세 [journal/2026-07-07.md](journal/2026-07-07.md)·[docs/33 §B-13u](33-file-ops-dnd-design.md).
- **B-2 셸 컨텍스트 메뉴 착수 — [ADR-0005](38-adr-0005-shell-context-menu.md) Accepted + S1 구현**: 행 우클릭 = **클래식 네이티브 셸 메뉴(`IContextMenu` HMENU) + 고유 항목 병합**(ID 대역 분리 셸 1~0x7FFF/고유 0x8000+ — 폴더에 붙여넣기·이름 바꾸기 F2·완전 삭제). `ShellContextMenu.cs` 신규(수동 COM 인터롭, IContextMenu2/3 메시지 포워딩="보내기"/"열기 방법", Shift=확장 동사, 셸 확장 예외 격리). 셸 명령 후 800ms 지연 재로드(watcher 보완). S2(빈영역 배경 메뉴)·S3(폴리시) 남음. 상세 [journal/2026-07-07.md](journal/2026-07-07.md).
- **☑ [BUG-009](BUGS.md) 해결 — 상승(관리자) 프로세스 OLE 드롭 폴백**: 원인 확정 — UAC OFF PC는 탐색기 실행도 전부 상승 + **WinUI 3가 상승 프로세스의 인바운드 드래그를 플랫폼에서 거부**(microsoft-ui-xaml#7690/#10119; 어제의 UIPI 배제 판단은 무효). 해결 — 상승 감지 시 XAML 브리지 HWND에 **고전 OLE `IDropTarget` 폴백**(신규 `OleDropTarget.cs`) 등록: CF_HDROP 추출·히트테스트(폴더 행/패널 현재 폴더)·기존 판정 규칙·전송 엔진 합류, 최적화 이동(원본 중복 삭제 방지). 비상승은 XAML 경로 유지. **탐색기→앱 복사 실기 확인**, 진단 코드 제거. 상세 [journal/2026-07-07.md](journal/2026-07-07.md).
- **🔴 [BUG-009](BUGS.md) 등록(긴급, [TODO B-16dnd](TODO.md))**: 외부(탐색기→앱) 드래그가 여전히 금지 커서 — UIPI 배제(일반 권한 재현), unpackaged WinUI 3의 StorageItems 미노출/DragOver 미도달 의심. **진단 로그 심음**(`%TEMP%\nexa-dnd-debug.log`) → 다음 세션 로그로 원인 분기. UIPI 함정(관리자 실행 시 DnD 차단)은 docs/33에 기록. (→ 같은 날 위 항목에서 해결)
- **Drag & Drop 전면 검토 → P1/P2/P3 개선**(3커밋): **외부(탐색기→앱) 파일 드롭 수신**(DND-EXT — 행·탭·빈 영역, deferral+StorageItems→기존 전송 엔진 합류, 복사 기본/Shift=이동; 기존 "수락 표시 후 무동작" 버그 해소) · **자기/하위 폴더 드롭 UI 차단**(DND-CYCLE) · **드래그 시작 StorageItem 병렬 취득**(대량 선택 지연 완화) · **외부 이동 드롭 후 패널 갱신** · 캡션 변경 시만 설정·문구 정리. 상세 [docs/33](33-file-ops-dnd-design.md) 07-07 절 · [journal/2026-07-07.md](journal/2026-07-07.md).
- **내장 터미널 UX 일괄 개선**(BP-T 후속): **캐럿 표시**([BUG-007](BUGS.md) ☑ — 오버레이 블록·포커스 시 깜빡임/비포커스 중공) · **클릭 포커스 안정화**(handledEventsToo+enqueue 재포커스) · **Tab 자동완성**(포커스 이동 차단·Shift+Tab 역방향) · **Backspace=DEL(0x7F)**(0x08=단어삭제 오매핑 수정) · **faint(SGR 2) 렌더**([BUG-008](BUGS.md) ◐ — PSReadLine 예측을 VS Code처럼 연한 회색으로) · **고정폭 셀 렌더**(Canvas 절대 배치 + 전각 2칸 — 캐럿/열 드리프트 해소) · **ECH(CSI X)·DECSC/DECRC** 구현(백스페이스 잔상 제거). 빌드 경고 0·에러 0. 상세 [journal/2026-07-07.md](journal/2026-07-07.md).

## 2026-07-06

- **🏷️ 릴리스 `0.2.0`** — `0.1.0` 이후 M1 대규모 진행분(파일 조작 전체·컬럼 정렬·타입어헤드·하단 패널[정보·미리보기·ConPTY 터미널]·미리보기 플러그인 SDK·문서 위키)을 묶어 태그·GitHub Release. 버전 동기화(Cargo/csproj 0.2.0). 아티팩트(MSIX/포터블)는 패키징 인프라 미비로 후속([12](12-packaging-portable.md)). 상세 [journal/2026-07-06.md](journal/2026-07-06.md).
- **전체 문서 통합 최신화**(다른 PC 병합분 정합): 이 PC 저장소 최신화(main FF 98커밋)·앱 빌드 green 후, 지연된 기준·참조 문서를 맞춤 — `CLAUDE.md`(현단계·구조·DR-6·다음단계)·`STATUS`(07-06 하단패널 블록)·`MILESTONES`(BP-2/터미널/플러그인 ☐→✅·M2 🚧)·`docs/16`·`docs/19`(카운트 34/57·ABI v7). 상세 [journal/2026-07-06.md](journal/2026-07-06.md).
- **하단 패널 콘텐츠 `feat/bottom-panel-info` → main 병합**(`3dd423a`): BP-2(정보·미리보기·플러그인 SDK) + BP-T(터미널).
  - **미리보기 시스템 + 플러그인**: 표준 `IPreviewProvider`+레지스트리(텍스트/이미지) · **퍼미시브 MIT SDK `Nexa.Plugins`**(DR-6)+샘플+**개발 매뉴얼**([36](36-plugin-development.md)) · 크기 상호연동 · 로딩 부하 방지 wrapper(디바운스/취소).
  - **임베디드 터미널**: **ConPTY** + **VT 에뮬레이터**(`VtScreen`: 색·화면버퍼·SGR·스크롤백, [37](37-terminal.md)) · lazy 로딩 · exit 재시작 · 작업경로=활성 탭 폴더(홈 폴백) · 키보드 캡처(전역 단축키 개입 차단). 알려진 이슈 [BUG-007/008](BUGS.md)(캐럿·색 → BP-T3).
  - 병합 후 `feat/bottom-panel-info` 로컬+원격 삭제. [BRANCHES](BRANCHES.md) 시간 역순으로 재정렬.
- 상세: [journal/2026-07-05.md](journal/2026-07-05.md)(BP-2/BP-T 세션) · [journal/2026-07-06.md](journal/2026-07-06.md).

## 2026-07-05

- **문서 통폐합**: 시간순 기록을 이 **DEVLOG**로, 기능·마일스톤 기록을 **MILESTONES**로 통합. 일자 단위·시간 역순 규약 도입. 브랜치 이력은 [BRANCHES.md](BRANCHES.md).
- **브랜치 정리**: 병합 완료 브랜치(refactor/001~003-audit·feat/bottom-panel) 로컬+원격 삭제. 이력은 BRANCHES.md/워크로그에 보존.
- **하단 패널 BP-1**(`feat/bottom-panel` → main `01c85a7`): 하단 도킹을 실제 콘텐츠 호스트로(`BottomDockView` — 정보/미리보기/Hex/터미널 종류 선택·스왑, 정보=현재 폴더) · **Ctrl+`** 표시/숨김 토글 · 하단 패널 상태(표시/높이/분리/종류) **session.json 저장·복원**. 미리보기/Hex/터미널(ConPTY)은 후속.
- **refactor/003-audit → main 병합**(`bd45f86`).
- **파일 전송 단일 엔진 통일**: DnD·붙여넣기(Ctrl+V·컨텍스트)가 `TransferPathsInto` 하나로 — **덮어쓰기 확인**(예/모두 예/건너뛰기/취소)·**바이트 진행률**·**진행 창**(맨앞·취소·자동닫기 off) · 확인 프롬프트를 진행 창 안에 embed(ContentDialog XamlRoot 오류 해결) · **OS 클립보드 붙여넣기**(탐색기 복사 인식) · 외부 DnD **StorageItems**(대상이 파일 열기)/Alt=경로.
- **탭 세션 저장/복원**(`session.json`, 요청/수행 분리·단일 Tick 코얼레싱) · **새로 만들기**(폴더/파일/바로가기) · **수정 날짜/시간 컬럼**(DateTime) · **경로 바 환경변수 해석**(%VAR%·$env:) · cargo fmt(CI 게이트) · 이 PC 식별 문서.
- 상세: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md)(세션 요약 2026-07-05) · [journal/archive/bottom-panel-worklog.md](journal/archive/bottom-panel-worklog.md).

## 2026-07-04

- **파일 조작 UX 심화 배치**(B-7~B-15h): 더블클릭 실행 · FileOps(맥 테스트) · 우클릭 컨텍스트 메뉴(폴더/파일/빈영역) · 복사/잘라/붙여/삭제 단축키 · DnD(폴더 이동+자동스크롤·좌우 패널·탭 hover·ESC 취소) · **디스크별 DnD**(같은=이동/다른=복사, Ctrl/Shift 강제) · 폴더 hover 진입 · **watcher 1차**(자동 갱신) · 확장자 컬럼. 버그(BUG-002~006).
- **DnD 탐색기 파리티**: 라이브 캡션("…에 복사/이동")·자기 폴더 드롭 규칙. 관리형 한계(DND-KEY/FONT/STACK)는 셸/OLE 트랙으로 보류.
- **헤더 정렬 COL-2/3**: 코어 비교자(SortKey/SortSpec)·ABI v6·UI(3상태 순환·▲▼·원문자 순번)·다중열(Shift)·좌우 패널 독립.
- **타입어헤드 설계 확정**([docs/32](32-typeahead-find.md)): A/B/C 범위·EphemeralOverlay.
- 상세: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md)(세션 요약 2026-07-04).

## 2026-07-03

- **refactor/003-audit 착수**: 4축 통합 감사(설계·성능·구조·FR/NFR) → 트랙 A~E 백로그. **B-6 인라인 이름 변경**(선택 후 재클릭/F2).
- **타입어헤드 TA-1/2/4·5**: 코어 `find_prefix`(ABI v7)·`TypeAheadBuffer`·앱 배선.
- **refactor/002-audit → main 병합**(`1d9d312`).
- 상세: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md) · [journal/archive/refactor-002-worklog.md](journal/archive/refactor-002-worklog.md).

## 2026-07-02

- **refactor/001-audit**: 1차 통합 감사 + **C1 코어 트리/선택 이관**(`nexa-tree` 가시행 스트림·C ABI·호스트 안전계층·앱 가상화 재배선·10만 노드 벤치·핸들 캐시) → **main 병합**(`b38e6b3`, 태그 `0.1.0`).
- **refactor/002-audit 착수**: 트랙 A 성능(백그라운드 열거·범위 diff·아이콘 LRU 캐시·경로→NodeId ABI v5) + 트랙 B 구조(PanelView·`Nexa.ViewModels`+C# xUnit).
- 상세: [journal/archive/refactor-001-worklog.md](journal/archive/refactor-001-worklog.md) · [journal/archive/refactor-002-worklog.md](journal/archive/refactor-002-worklog.md).

## 2026-07-01

- **레이아웃 정교화·플래그십 초안**(F7~F19): 좌/우 듀얼 목록·하단 도킹 연동 · `NexaFileGrid` 추출 · 스플리터 자석 스냅 · **인라인 폴더 펼침 + Finder식 컬럼** · 파일 선택(단일/Ctrl/Shift) · 키보드 이동·펼침·캐럿 · **패널별 탭**·닫기 · 네비(뒤로/앞으로/위로·Alt) · 계층 경로 바 `NexaPathBar` · 숨김/점 파일 토글.
- 빌드 이슈 해결(Sizers TFM 22621 정합).

## 2026-06-30

- **킥오프**: 비전·요구 확장·설계·ADR(DR-1 WinUI3+Rust 코어·DR-5 라이선스)·플러그인/터미널/런처 설계·라이선스 인증 설계·컨텍스트 공유·개발 방법론.
- **스캐폴딩 + 환경 검증**: `core/`·`app/Nexa.App`·CI(mac/win)·LICENSE·bootstrap. Windows 풀빌드 실측.
- **M0 데이터 흐름 수직 슬라이스**: 인터롭 PoC → nexa-vfs 스트리밍 열거 → 인터롭 디렉터리 열거 → C# 바인딩+UI → ItemsRepeater 가상화 → 레이아웃 골격(7행 그리드). `0.1.0` 태그 기준.
- 상세: [journal/](journal/) 2026-06-30 세션들.
