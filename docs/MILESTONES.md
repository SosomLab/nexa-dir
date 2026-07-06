# MILESTONES — 기능·마일스톤 기록 (프로젝트 목적 기준)

> **프로젝트 목적(차세대 Windows 파일 탐색기) 기준으로 기능과 마일스톤을 관찰**하는 단일 기록.
> 시간순 진행은 짝 문서 **[DEVLOG.md](DEVLOG.md)** 참조. 로드맵 원안 [02](02-roadmap.md)·기능 명세 [03](03-features.md)·요구 [05](05-requirements.md).
> 상태: ✅ 완료 · 🚧 구현·QA대기 · 📐 설계 · ☐ 미착수. (WinUI는 맥 빌드 불가 → "구현"=로컬/CI 빌드 green, 런타임은 Windows 실기 QA.)

## 마일스톤 개요

| # | 목표 | 상태 |
| --- | --- | --- |
| **M0** | 기반(인터롭·스트리밍 열거·가상화 렌더·CI/부트스트랩) | ✅ 완료 (`0.1.0`) |
| **M1** | ★ 1순위 묶음 — 경로 바·탭/듀얼·플래그십(인라인 트리+교차선택)·컨텍스트 메뉴·퀵 런처 | 🚧 **진행 중**(대부분 구현, QA·잔여) |
| **M2** | 미리보기 | 🚧 하단 패널 텍스트·이미지 미리보기 + 플러그인 SDK 구현(BP-2). 추가 포맷·전용 뷰 후속 |
| **M3** | 검색(tantivy) | ☐ 설계 일부([24](24-search.md)) |
| **M4** | 클라우드/원격(SMB→SFTP→S3→SaaS) | ☐ |
| **M5** | AI | ⏸ 보류(별도 ADR) |
| **M6** | 플러그인(WASM+Python/Node) | ☐ 설계([09](09-plugin-architecture.md)) |
| **M7** | 라이선스 정품 인증(Ed25519) | 📐 설계([17](17-licensing-activation.md)) |

---

## M0 — 기반 ✅

인터롭 왕복 PoC · nexa-vfs 스트리밍 열거 · 인터롭 디렉터리 열거 API · C# 바인딩+UI · ItemsRepeater 가상화 · 레이아웃 골격 · CI(mac/win)+bootstrap. 태그 `0.1.0`.

## M1 — 1순위 묶음 🚧

### 데이터·코어
- ✅ 코어 트리/선택(C1, `nexa-tree` 가시행 스트림+OrderedSet, C ABI) — 10만 노드 벤치 프레임 예산 내.
- ✅ 성능: 백그라운드 열거·범위 diff 통지·아이콘 LRU 캐시+로딩 큐·경로→NodeId 조회.
- ✅ 정렬 코어 비교자(SortKey/SortSpec, ABI v6).

### 플래그십(인라인 트리 + 교차 선택)
- ✅ 인라인 폴더 펼침 + Finder식 컬럼 · 펼침 상태 유지(경로 기준).
- ✅ 파일 선택(단일/Ctrl 다중/Shift 범위)·키보드(범위·비연속·→/←·캐럿).
- 🚧 교차폴더 다중 선택 완성(C3/C4)·러버밴드 드래그 선택 — 후속.

### 레이아웃·네비게이션
- ✅ 좌/우 듀얼 패널 · 패널별 탭(멀티라인·닫기 Ctrl+W) · 스플리터 자석 스냅.
- ✅ 계층 경로 바 `NexaPathBar`(세그먼트 이동·우클릭 편집·**환경변수 해석** %VAR%/$env:).
- ✅ 네비(뒤로/앞으로/위로·Alt·마우스 X버튼) · 숨김/점 파일 토글.
- ✅ 타입어헤드 찾기(코어 find_prefix·버퍼·배선).

### 파일 조작 (전송 단일 엔진 `TransferPathsInto`)
- ✅ 복사/이동/삭제(휴지통·완전)·인라인 이름변경(F2)·**새로 만들기**(폴더/파일/바로가기).
- ✅ 컨텍스트 메뉴(폴더/파일/빈영역)·단축키(Ctrl+C/X/V·Del).
- ✅ DnD(폴더 이동·좌우 패널·탭/폴더 hover·ESC·디스크별 기본·외부 앱에 **파일 열기** StorageItems·Alt=경로).
- ✅ **덮어쓰기 확인**(예/모두 예/건너뛰기/취소)·**바이트 진행률 + 진행 창**(맨앞·취소·자동닫기 off).
- ✅ **OS 클립보드 붙여넣기**(탐색기 복사 인식, 읽기측) · 앱 클립보드 최신 우선.
- ✅ 자동 갱신 watcher 1차(FolderWatcher).
- 🚧 QA 대기 다수 · 📐 Undo/Redo(B-13u)·nexa-ops 이관 후속 · ☐ CLIP 쓰기측(우리 복사→탐색기).

### 컬럼
- ✅ 이름·확장자·**수정 날짜/시간(DateTime)**·종류·크기 · 헤더 정렬(3상태·다중열 Shift·좌우 독립).
- 🚧 Date/Time 개별 선택 컬럼(COL-D3/D4)·컬럼 조정 모달(COL-4) — 후속.

### 하단 패널 (feat/bottom-panel → feat/bottom-panel-info, BP-1/2/T ✅)
- ✅ 콘텐츠 호스트(정보/미리보기/Hex/터미널 종류 선택·스왑) · Ctrl+` 토글 · 상태 세션 저장/복원 (BP-1).
- ✅ **정보 뷰** = 선택 항목 속성 (BP-2 슬라이스 1).
- ✅ **미리보기**(텍스트·이미지) — 표준 공급자 + 플러그인. **미리보기 플러그인 SDK `Nexa.Plugins`(퍼미시브 MIT, DR-6)** + 샘플 플러그인 분리, 설계 [35](35-preview-system.md)·매뉴얼 [36](36-plugin-development.md) (BP-2).
- ✅ **임베디드 터미널** ConPTY(lazy) + **VT 에뮬레이터**(색/화면 버퍼·exit 재시작·선택 탭 cwd·전역 단축키 차단 키보드 캡처), 설계 [37](37-terminal.md) (BP-T).
- ✅ **터미널 UX 개선(07-07)**: 캐럿 표시([BUG-007](BUGS.md) ☑)·클릭 포커스·Tab 완성/Backspace 매핑·faint 예측([BUG-008](BUGS.md) ◐)·고정폭 셀 렌더(전각 2칸)·ECH/커서저장복원.
- 🚧 Hex 뷰 · 🐛 잔여: 일부 확장 SGR·파워라인 글리프(Nerd Font)([BUG-008](BUGS.md)).

### 세션·설정
- ✅ 탭 세션 저장/복원(`session.json`, 요청/수행 분리·Tick 코얼레싱) · 하단 패널 상태.
- 🚧 일반 설정 영속화(`settings.json`)·설정 UI · 창 위치 복원([28](28-startup-memory-optimization.md)) — 설계됨, 구현 대기.

### 미착수(M1 잔여)
- ☐ **컨텍스트 메뉴 셸 통합**(`IContextMenu`/`IExplorerCommand`, SHELL) — 현재 고유 메뉴만, 셸 항목 병합은 COM ADR 후보.
- ☐ **퀵 런처 바**(외부 터미널/에디터, 현재 placeholder). (하단 임베디드 터미널은 BP-T로 ✅ 구현.)

---

## M2+ (요약)

- **M2 미리보기**: 하단 패널 BP-2에서 일부 착수 예정.
- **M3 검색**: tantivy 인덱스([24](24-search.md)).
- **M4 클라우드/원격** · **M5 AI**(보류) · **M6 플러그인**([09](09-plugin-architecture.md)) · **M7 라이선스 인증**([17](17-licensing-activation.md)).

> 진행 상세·근거는 [DEVLOG.md](DEVLOG.md)와 각 설계 문서. 알려진 이슈 [BUGS.md](BUGS.md), 실기 QA [QA-003-checklist.md](QA-003-checklist.md).
