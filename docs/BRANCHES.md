# BRANCHES — 브랜치 기록 (Branch History, 시간 역순)

> **목적**: 병합 후 삭제되는 작업 브랜치의 이력을 남긴다. 각 브랜치가 **언제 생성**되고 **무슨 작업**을 했으며
> **어떤 커밋**이 있었고 **언제 main에 병합**되고 **언제 삭제**되었는지 추적한다.
> **규약**: 브랜치는 main 병합·빌드 green 확인 후 **로컬+원격 삭제**하고, 이력은 이 문서 + 각 브랜치 워크로그(journal)에 보존.
> **정렬**: **시간 역순(최신이 위)**. 새 브랜치를 만들면 표·상세 모두 **맨 위에 추가**한다. 시각=커밋 committer date(KST).

## 요약 (시간 역순)

| 브랜치 | 생성 | 병합(커밋) | 삭제 | 커밋수 | 작업 요약 | 상세 |
| --- | --- | --- | --- | --- | --- | --- |
| `feat/theme-system` | 2026-07-08 | 2026-07-08 | 2026-07-08 | 3+docs | 테마 S1(틴트 제거·토큰·라이트/다크) + 상태바 정리 + 가로 스크롤 | [2026-07-08](journal/2026-07-08.md)·[docs/39](39-theme-system.md) |
| `feat/bottom-panel-info` | 2026-07-05 | 2026-07-06 (`3dd423a`) | 2026-07-06 | 9 | 하단 콘텐츠 — 정보/미리보기(플러그인 SDK)·터미널(BP-2/BP-T) | [2026-07-05](journal/2026-07-05.md) |
| `feat/bottom-panel` | 2026-07-05 | 2026-07-05 (`01c85a7`) | 2026-07-05 | 4 | 하단 패널 컨테이너 프레임워크(BP-1) | [worklog](journal/archive/bottom-panel-worklog.md) |
| `refactor/003-audit` | 2026-07-03 | 2026-07-05 (`bd45f86`) | 2026-07-05 | 77 | 3차 감사 — 파일 조작·전송 엔진·정렬·타입어헤드 | [worklog](journal/archive/refactor-003-worklog.md) |
| `refactor/002-audit` | 2026-07-02 | 2026-07-03 (`1d9d312`) | 2026-07-05 | — | 2차 감사 — 트랙 A 성능 + B 구조 | [worklog](journal/archive/refactor-002-worklog.md) |
| `refactor/001-audit` | 2026-07-02 | 2026-07-02 (`b38e6b3`, 태그 `0.1.0`) | 2026-07-05 | — | 1차 감사 + **C1 코어 트리/선택 이관** | [worklog](journal/archive/refactor-001-worklog.md) |

> 참고: 스트레이 로컬 브랜치 `a`(= 002 병합 커밋 `1d9d312`를 가리키던 실수 브랜치, 고유 커밋 0)도 2026-07-05 정리 삭제.

---

## feat/theme-system

- **생성**: 2026-07-08 (분기: main `379a8aa`). **3커밋 + docs**. 병합·삭제: 2026-07-08.
- **작업**: **테마 시스템 S1**([docs/39](39-theme-system.md)) — 영역 구분 틴트 전면 제거 → 시맨틱 토큰 10종(App.xaml ThemeDictionaries)·라이트 팔레트 정비·구성(O) 메뉴 라이트/다크/시스템 전환(기본 Light)·후속 세부 설정 UI(테마팩·폰트·밀도) 설계.
- **동반**: 패널 보기 토글 → 표시(S) 메뉴 이관(상태바=상태 전용, `c5df6b9`) · 파일 목록 **가로 스크롤**(넘칠 때만·헤더 양방향 동기, `a099352`).
- 사용자 QA: 라이트 배치 확인("전반적으로 깔끔").

## feat/bottom-panel-info

- **생성**: 2026-07-05 (분기: main `01c85a7` 이후). **9 커밋**.
- **작업**: 하단 패널 **콘텐츠 실체화**.
  - **BP-2 정보/미리보기**: 정보 뷰(선택 항목 속성) · **미리보기 시스템**(표준 `IPreviewProvider`+`PreviewRegistry`, 텍스트/이미지) ·
    **퍼미시브 MIT 플러그인 SDK `Nexa.Plugins`**(DR-6)+샘플 `Nexa.Plugins.Samples`+**개발 매뉴얼**([36](36-plugin-development.md)) ·
    크기 상호연동(`PreviewRequest`) · **로딩 부하 방지 wrapper**(디바운스/취소/중복스킵) · 텍스트 1줄 버그·가로세로 스크롤 수정.
  - **BP-T 터미널**: **ConPTY 세션** + **VT 에뮬레이터**(`VtScreen`: 색·화면 버퍼·커서·SGR·스크롤백) · **lazy 로딩** · **exit 시 재시작** ·
    작업경로=활성 탭 폴더(홈 폴백) · **키보드 캡처**(전역 단축키 개입 차단). 알려진 이슈 [BUG-007/008](BUGS.md)(캐럿·색, BP-T3).
- **병합**: 2026-07-06 (`3dd423a`) → main. 코어·앱 빌드 green.
- **삭제**: 2026-07-06 (로컬 + 원격 `origin/feat/bottom-panel-info`).
- **상세**: [journal/2026-07-05.md](journal/2026-07-05.md) · 설계 [35](35-preview-system.md)·[36](36-plugin-development.md)·[37](37-terminal.md).

## feat/bottom-panel

- **생성**: 2026-07-05 17:47 (첫 커밋 `133dc0d`). 분기: `bd45f86`(003 병합 직후).
- **작업**: **하단 패널 컨테이너 프레임워크(BP-1)** — placeholder → 실제 콘텐츠 호스트. 콘텐츠 종류 선택(정보/미리보기/
  Hex/터미널)·스왑(정보=현재 폴더 실제, 나머지 준비 중) · **Ctrl+\` 표시/숨김 토글** · 하단 패널 상태(표시/높이/좌우
  분리/콘텐츠 종류) **session.json 저장·복원**. 미리보기/Hex/터미널(ConPTY)은 후속(BP-2/BP-T).
- **커밋 이력** (4):
  - `133dc0d` docs(bottom-panel): 하단 패널 구현 브랜치 작업 로그 — 분해·BP-1 계획
  - `8710afa` feat(app): 하단 도킹 콘텐츠 호스트 BottomDockView (BP-1a)
  - `306f476` feat(app): 하단 패널 Ctrl+\` 토글 + 상태 세션 저장/복원 (BP-1b/1c)
  - `704d125` docs(bottom-panel): BP-1 프레임워크(호스트·Ctrl+\` 토글·세션 저장) 진행 기록
- **병합**: 2026-07-05 18:02 (`01c85a7`) → main. 코어·앱 빌드 green.
- **삭제**: 2026-07-05 (로컬 + 원격 `origin/feat/bottom-panel`).
- **상세**: [journal/archive/bottom-panel-worklog.md](journal/archive/bottom-panel-worklog.md).

## refactor/003-audit

- **생성**: 2026-07-03 20:07 (첫 커밋 `7997efc` — 3차 검증 4축 감사). 분기: `1d9d312`(002 병합 직후). **77 커밋**.
- **작업**: 3차 감사 라운드 — **파일 조작 계층**(인라인 이름변경 B-6 · 컨텍스트 메뉴 · 복사/이동/삭제 · 단축키 ·
  DnD 폴더 이동/좌우/탭·폴더 hover/ESC/디스크별 · 휴지통 삭제) · **DnD 탐색기 파리티**(라이브 캡션·자기폴더 규칙) ·
  **헤더 정렬 COL-2/3**(3상태·다중열·좌우 독립·ABI v6) · **확장자/날짜·시간 컬럼** · **타입어헤드**(코어 find_prefix·버퍼·배선) ·
  **watcher 1차** · **탭 세션 저장/복원**(session.json, 요청/수행 분리·Tick 코얼레싱) · **경로 바 환경변수 해석** ·
  **전송 단일 엔진 `TransferPathsInto`**(덮어쓰기 확인·바이트 진행률·진행 창·취소) · **OS 클립보드 붙여넣기** ·
  **새로 만들기**(폴더/파일/바로가기) · **새 PC 식별 문서**.
- **병합**: 2026-07-05 17:31 (`bd45f86`) → main. 로컬 빌드·테스트 green(코어 · ViewModels xUnit 57 · 앱 0/0).
- **삭제**: 2026-07-05 (로컬 + 원격 `origin/refactor/003-audit`).
- **상세 커밋 이력**: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md).

## refactor/002-audit

- **생성**: 2026-07-02 23:32 (첫 커밋 `5d676ae` — 진행 로그 스캐폴드). 분기: `b38e6b3`(001 병합 직후).
- **작업**: 2차 감사(전체 개발범위 5축) → **트랙 A 성능**(A-3 경로→NodeId ABI v5 · A-1 백그라운드 열거 · A-2 범위 diff 통지 ·
  A-4 아이콘 LRU 캐시+로딩 큐) + **트랙 B 구조**(PanelView 그룹 객체 · `Nexa.ViewModels`(net8.0) 추출 + C# xUnit 도입) + 문서·QA. BUG-001 해결.
- **병합**: 2026-07-03 19:51 (`1d9d312`) → main.
- **삭제**: 2026-07-05 (원격 `origin/refactor/002-audit`).
- **상세 커밋 이력**: [journal/archive/refactor-002-worklog.md](journal/archive/refactor-002-worklog.md).

## refactor/001-audit

- **생성**: 2026-07-02 13:19 (첫 커밋 `41bb9e2` — 통합 감사 진단). 분기: main(스캐폴딩 이후).
- **작업**: 1차 통합 감사(소스·문서·진행 정합) → **C1 코어 트리/선택을 Rust 코어(`nexa-tree`)로 이관** — 가시행 평면
  스트림 + OrderedSet 선택, C ABI `nexa_tree_*`, 호스트 ABI 안전 계층, 앱 가상화 재배선(펼침 유지·스크롤 복원),
  10만 노드 코어 벤치, 탭별 트리 핸들 캐시, id→가시 인덱스 조회, 죽은 코드 정리.
- **병합**: 2026-07-02 23:20 (`b38e6b3`) → main. 병합 전 베이스라인에 태그 `0.1.0`.
- **삭제**: 2026-07-05 (원격 `origin/refactor/001-audit`).
- **상세 커밋 이력**: [journal/archive/refactor-001-worklog.md](journal/archive/refactor-001-worklog.md).
