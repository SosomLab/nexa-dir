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
