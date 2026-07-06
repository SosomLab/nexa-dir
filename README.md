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

- 단계: **M1 진행**(M0 완료·`0.1.0`). 인라인 트리·듀얼 패널/탭·계층 경로 바·**파일 조작**(복사/이동/삭제/이름변경/새로 만들기·컨텍스트 메뉴·클립보드·DnD 전송엔진)·**컬럼 정렬**·**타입어헤드**·**하단 패널**(정보·미리보기·ConPTY 터미널)·**미리보기 플러그인 SDK** 구현.
- 남은 M1: 교차폴더 선택 완성·Undo/Redo·설계 계약(에러/Provider/watcher)·다수 실기 QA.
- 상세 현황 → [docs/STATUS.md](docs/STATUS.md) · 기능·마일스톤 → [docs/MILESTONES.md](docs/MILESTONES.md).

## 문서 — 📖 [문서 홈 (Wiki)](docs/README.md)에서 시작

문서가 많아 **[docs/README.md](docs/README.md)** 를 길잡이(Home)로 두었습니다.
거기서 **추천 읽기 순서**(시발점 → 주요 목표 → 진행 경과[시간 역순] → 상세)와 **전체 색인**을 따라 이동할 수 있습니다.

바로가기: [현황 STATUS](docs/STATUS.md) · [기능·마일스톤 MILESTONES](docs/MILESTONES.md) · [진행 경과 DEVLOG](docs/DEVLOG.md) · [비전 00](docs/00-vision.md) · [결정 기록 10](docs/10-decision-record.md) · [★ 플래그십 07](docs/07-flagship-tree-multiselect.md) · [이식 메모리 CLAUDE.md](CLAUDE.md)

## 프로젝트 정보 / 연락처

| 항목 | 내용 |
| --- | --- |
| 조직 | **SosomLab** — <https://sosomlab.com> |
| 저장소 | <https://github.com/SosomLab/nexa-dir> |
| 상업 라이선스·문의 | **kiros33@sosomlab.com** (상업적 사용은 유료 라이선스) |
| 개발자(maintainer) | Sangyong Bae — **kiros33@gmail.com** |

> 본 저장소는 **공개 예정**(현재 private, 어느 정도 진행 후 public 전환)이며, 공개 시 위 연락처도 함께 공개됩니다(상업 라이선스/기여 문의용).

## 라이선스

**소스공개 제한형 — PolyForm Noncommercial 1.0.0**
(영문 정본 [LICENSE.md](LICENSE.md) · 한글본 [LICENSE.ko.md](LICENSE.ko.md), 대안 BSL 1.1).
개인·비상업 사용 무료, **상업적 사용은 유료 라이선스**(문의 kiros33@sosomlab.com). 저장소는 **공개 예정**(현재 private, 진행 후 public 전환).
의존성 고지: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) · 정책: [docs/13-licensing.md](docs/13-licensing.md).
