# 📖 Nexa Dir — 문서 홈 (Wiki)

> **처음 보는 사람을 위한 길잡이.** 아래 **추천 읽기 순서**를 위에서 아래로 따라가면
> _시발점 → 주요 목표 → 진행 경과(시간 역순) → 상세_ 순으로 프로젝트를 이해할 수 있다.
> 각 링크로 이동해 읽고, 다 보면 이 **Home**으로 돌아오면 된다. 프로젝트 한 줄 소개·설치는 루트 [README](../README.md).
>
> **한 장 현황이 급하면 →** [STATUS](STATUS.md) · **최근 무슨 일 →** [DEVLOG](DEVLOG.md) · **기능 현황 →** [MILESTONES](MILESTONES.md).

---

## 🧭 추천 읽기 순서 (순서대로)

1. **왜 만드나** — [00 비전](00-vision.md) : 문제의식·목표·차별화
2. **무엇을 만드나** — [05 요구사항](05-requirements.md) : 기능(FR)/비기능(NFR)/제약
3. **핵심 결정** — [10 결정 기록](10-decision-record.md) : 스택·라이선스·플러그인 SDK 등(DR-1~6 + ADR 색인)
4. **★ 대표 기능** — [07 플래그십](07-flagship-tree-multiselect.md) : 인라인 폴더 확장 + 폴더 교차 다중 선택
5. **어떻게 짓나** — [01 아키텍처](01-architecture.md) → [02 로드맵](02-roadmap.md)
6. **지금 상태** — [STATUS](STATUS.md) (한 장) → [MILESTONES](MILESTONES.md) (기능·마일스톤 현황)
7. **진행 경과** — [DEVLOG](DEVLOG.md) (시간 역순) → 관심 날짜 [journal/](journal/)
8. **관심 주제 깊이 읽기** — 아래 [⑤ 주제별 상세](#-주제별-상세-설계-문서)

---

## ① 시발점 · 정체성 (왜 이 프로젝트인가)

| 문서 | 내용 |
|---|---|
| [00 비전](00-vision.md) | 비전·목표·경쟁 제품 분석·차별화 |
| [05 요구사항](05-requirements.md) | 전체 요구(기능/비기능/제약/리스크) |
| [08 경쟁 조사](08-competitive-feature-survey.md) | Win/Mac 탐색기·커맨더 기능 조사 & 채택 추천 |
| [10 결정 기록](10-decision-record.md) | ★ 확정 결정(DR-1~6)·ADR 색인 |
| [06 ADR-0001 스택](06-adr-0001-stack.md) · [13 라이선스](13-licensing.md) | 언어/스택 선택 근거 · 라이선스 방향 |
| [CLAUDE.md](../CLAUDE.md) | 이식용 프로젝트 메모리(다른 PC clone 시 즉시 컨텍스트) |

## ② 주요 목표 · 설계 (무엇을, 어떻게)

| 문서 | 내용 |
|---|---|
| [02 로드맵](02-roadmap.md) | 단계별 계획 M0~M7 (원안) |
| [MILESTONES](MILESTONES.md) | ★ 기능·마일스톤 **현황**(단일 출처, ✅/🚧/📐/☐) |
| [07 플래그십](07-flagship-tree-multiselect.md) | ★ 인라인 트리 + 교차폴더 다중 선택 |
| [01 아키텍처](01-architecture.md) | 코어(Rust)/UI(WinUI)/인터롭 모듈 구조 |
| [03 기능 명세](03-features.md) · [04 트렌드 백로그](04-trends-todo.md) | 기능 명세 · 지속 갱신 아이디어 |

## ③ 진행 경과 (시간 역순)

| 문서 | 내용 |
|---|---|
| [DEVLOG](DEVLOG.md) | ★ 개발 진행 시간순(**최신이 위**), 날짜별 요약 |
| [journal/](journal/) | 일자별 상세 `YYYY-MM-DD.md`(파일 내부도 시간 역순) |
| [BRANCHES](BRANCHES.md) | 브랜치 생성/작업/병합/삭제 이력(시간 역순) |
| [journal/archive/](journal/archive/) | 통폐합 이전 라운드 워크로그·감사 리포트(상세 근거) |

## ④ 현재 상태 · 할 일 · 품질

| 문서 | 내용 |
|---|---|
| [STATUS](STATUS.md) | ★ 현재 상태 한 장(결정·구현·다음 단계) |
| [TASKS](TASKS.md) | 작업 원장(추가/설계/개발/테스트 일시 추적) |
| [TODO](TODO.md) | 할 일 백로그(트랙별·범위 산정·append) |
| [19 구현 기능 현황](19-implemented-features.md) | 초기 F1~F28 검증 상세(이후는 MILESTONES) |
| [BUGS](BUGS.md) | 알려진 이슈 · [QA-003](QA-003-checklist.md) QA 체크리스트 |

## ⑤ 주제별 상세 (설계 문서)

**아키텍처 결정(ADR)** — [06 ADR-0001 스택](06-adr-0001-stack.md) · [21 ADR-0002 재사용 파일뷰](21-adr-0002-fileview-control.md) · [22 ADR-0003 뷰/패널 모듈](22-adr-0003-view-and-panel-modules.md) · [29 ADR-0004 코어 트리 모델](29-adr-0004-core-tree-model.md)

**서브시스템 · 기능**
| 문서 | 내용 |
|---|---|
| [09 플러그인 아키텍처](09-plugin-architecture.md) | 플러그인 호스팅(WASM/Python/Node/Lua) 방향 |
| [35 미리보기 시스템](35-preview-system.md) · [36 플러그인 개발](36-plugin-development.md) | 미리보기 공급자·플러그인 SDK(`Nexa.Plugins`, MIT) |
| [37 터미널](37-terminal.md) | 임베디드 ConPTY 터미널 · VT 에뮬레이터 |
| [23 컬럼 시스템](23-column-system.md) · [32 타입어헤드](32-typeahead-find.md) | 컬럼·정렬 · 타이핑 이동 |
| [33 파일 조작·DnD](33-file-ops-dnd-design.md) · [34 설정·세션 영속화](34-settings-and-session-persistence.md) | 전송 엔진·드래그앤드롭 · 설정/세션 |
| [24 검색](24-search-everything.md) · [25 일괄 리네임](25-bulk-rename.md) · [26 커맨드 팔레트](26-command-palette.md) | 인스턴트 검색 · 배치 리네임 · 명령 팔레트 |
| [27 경로 바](27-pathbar-component.md) · [20 UI 레이아웃](20-ui-layout.md) | NexaPathBar · 레이아웃 골격 |
| [28 시작·메모리 최적화](28-startup-memory-optimization.md) · [28 창위치/세션 복원](28-window-session-restore.md) · [31 스크롤 노트](31-scroll-into-view-notes.md) | 상주 규율·기동 · 창 복원 · 스크롤 |

**배포 · 라이선스 · 용어** — [12 포터블 패키징](12-packaging-portable.md) · [17 라이선스 정품 인증](17-licensing-activation.md) · [30 용어집](30-glossary.md)

## ⑥ 개발 · 기여

| 문서 | 내용 |
|---|---|
| [11 개발 환경](11-dev-environment.md) | 맥(코어)/Windows(앱)/CI · 부트스트랩 |
| [18 빌드 & 테스트](18-build-and-test.md) | ★ 부문별 빌드·테스트 절차(SSOT) |
| [16 프로젝트 구조](16-project-structure.md) | 폴더/프로젝트/크레이트별 목적 |
| [15 개발 방법론](15-dev-methodology.md) · [14 컨텍스트 공유](14-context-sharing.md) | 수직 슬라이스·커밋 규약 · 비밀 관리 |

---

> 문서 규약(2026-07-05~): 진행 기록은 **일자 단위** — 하루 상세 `journal/YYYY-MM-DD.md`(시간 역순), 요약은 [DEVLOG](DEVLOG.md)(시간순)·[MILESTONES](MILESTONES.md)(기능). 결정은 [10](10-decision-record.md), 빌드/테스트 SSOT는 [18](18-build-and-test.md).
