# Nexa Dir — 차세대 Windows 탐색기

> Next-generation native file explorer for Windows. 성능은 네이티브, 경험은 차세대.

Nexa Dir은 Windows 기본 파일 탐색기(File Explorer)를 대체하는 것을 목표로 하는
**네이티브 고성능 파일 관리자**입니다. 탭과 듀얼 패널을 기본으로, 빠른 미리보기,
클라우드·원격 통합, AI 기반 검색·정리까지 단계적으로 확장합니다.

## 설계 원칙

1. **성능 최우선 (Native-first)** — 파일 열거/검색/미리보기 등 핫패스는 네이티브 코어로 처리.
2. **즉시 반응 (Instant UI)** — 100k+ 항목 폴더도 가상화로 즉시 렌더. 백그라운드 작업이 UI를 막지 않음.
3. **모듈형 (Modular)** — 코어 엔진 / VFS(가상 파일시스템) / 인덱서 / UI 셸을 분리해 기능 확장 비용 최소화.
4. **안전 (Safe by default)** — 파괴적 작업은 트랜잭션·휴지통·실행취소 보장.
5. **상주 규율 (Always-on discipline)** — 켜두고 쓰는 앱. 저메모리·주기적 정리·무간섭,
   하위시스템 오류가 앱 전체나 다른 프로그램을 방해하지 않음.

## 현재 상태

- 단계: **설계/계획** (코드 미구현)
- 구현 1순위: **탭 + 듀얼 패널** (Milestone 1)

## 문서

| 문서 | 내용 |
| --- | --- |
| [docs/00-vision.md](docs/00-vision.md) | 비전 · 목표 · 경쟁 제품 분석 · 차별화 |
| [docs/01-architecture.md](docs/01-architecture.md) | 기술 스택 결정 · 시스템 아키텍처 · 모듈 구조 |
| [docs/02-roadmap.md](docs/02-roadmap.md) | 단계별 로드맵 (Milestone 1: 탭/듀얼 패널) |
| [docs/03-features.md](docs/03-features.md) | 기능 명세 (미리보기 · 클라우드 · AI) |
| [docs/04-trends-todo.md](docs/04-trends-todo.md) | 최신 트렌드 백로그 (지속 갱신) |
| [docs/05-requirements.md](docs/05-requirements.md) | 전체 요구사항 (기능/비기능/제약/리스크) |
| [docs/06-adr-0001-stack.md](docs/06-adr-0001-stack.md) | 언어/스택 결정 분석 (개발 전 확정) |
| [docs/07-flagship-tree-multiselect.md](docs/07-flagship-tree-multiselect.md) | ★ 인라인 폴더 확장 + 폴더 교차 다중 선택 |
| [docs/08-competitive-feature-survey.md](docs/08-competitive-feature-survey.md) | Mac/Win 탐색기 기능 조사 & 채택 추천 |
| [docs/09-plugin-architecture.md](docs/09-plugin-architecture.md) | 플러그인 아키텍처 (Python/Node/Lua/WASM) |
| [docs/10-decision-record.md](docs/10-decision-record.md) | ★ 통합 결정 기록 (스택·디자인·배포 확정) |
| [docs/11-dev-environment.md](docs/11-dev-environment.md) | 개발 환경 구성 (Mac 코어 / Windows 앱 / CI) |
| [docs/12-packaging-portable.md](docs/12-packaging-portable.md) | 패키징 & 포터블(단일 exe) 배포 설계 |
| [docs/13-licensing.md](docs/13-licensing.md) | 라이선스 방향 검토 (유료 판매 가능) |
| [docs/journal/](docs/journal/) | 작업 기록 (질문·목표·답변·진행, `YYYYMMDD_HHMMSS` 타임스탬프) |

## 라이선스

TBD
