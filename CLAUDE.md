# CLAUDE.md — Nexa Dir 프로젝트 컨텍스트 (이식용 메모리)

> 이 파일은 **다른 PC에서 clone 시 즉시 컨텍스트를 공유**하기 위한 휴대용 프로젝트 메모리다.
> Claude Code는 저장소 루트의 `CLAUDE.md`를 자동 로드하므로, 새 세션/새 PC에서도 여기서 출발하면 된다.
> **먼저 읽기:** [docs/STATUS.md](docs/STATUS.md)(현황) → [docs/10-decision-record.md](docs/10-decision-record.md)(결정).

## 1. 이 프로젝트는

**Nexa Dir** = 차세대 **Windows 파일 탐색기**(Windows 기본 탐색기 대체). 네이티브 고성능 + 프로툴 UX.
원격: `SosomLab/nexa-dir`. 현 단계: **설계/계획 완료, 개발 착수 직전(코드 미구현)**.

## 2. 확정 결정 (변경 시 새 ADR/journal)

| # | 영역 | 결정 |
| --- | --- | --- |
| DR-1 | 스택 | **Rust 코어(cdylib) + WinUI 3(C#/.NET8)**, 인터롭 C ABI/csbindgen (ADR-0001 Accepted) |
| DR-2 | 디자인 | **프로툴 고밀도(Path Finder풍)**, 다크 기본, 키보드 우선 |
| DR-3 | 배포 | **MSIX + Releases + winget** + 포터블(폴더/단일exe) 가능 설계 |
| DR-4 | AI | 보류 (M5 전 별도 ADR) |
| DR-5 | 라이선스 | **소스공개 제한형 PolyForm Noncommercial**(대안 BSL), public, 개인무료/상업유료 |

## 3. 핵심 요구 (우선순위)

- ★ **플래그십(M1)**: macOS식 **인라인 폴더 확장** + **폴더 교차 다중 선택** + 혼합 일괄 작업 → [docs/07](docs/07-flagship-tree-multiselect.md)
- **좌/우 듀얼 패널 + 패널별 싱글/멀티라인 탭**(M1), **상단 계층 경로 바(우선)**, **컨텍스트 메뉴**(셸 호스팅+고유)(M1)
- **퀵 런처 바**(M1) + **하단 임베디드 터미널**(ConPTY, 설계동결/구현후속)
- **상주 규율 NFR**: 저메모리·주기정리·무간섭·오류격리
- 후속: 미리보기(M2)·검색(M3)·클라우드/네트워크(M4)·AI(M5)·플러그인(M6, WASM+Python/Node)·내장 zip(향후)

## 4. 아키텍처 요약 ([docs/01](docs/01-architecture.md))

- **핫패스는 Rust 코어**(VFS·인덱스 tantivy·프리뷰·ops·pty·plugin 호스트 wasmtime/mlua),
  **UI는 WinUI 3(C#)**. **셸 COM/컨텍스트메뉴는 C# 계층**, 플래그십 트리는 **가시 노드 평면 스트림 + 가상화**.
- 리포 구조(예정): `core/`(Rust 워크스페이스) · `app/`(WinUI 솔루션) · `docs/` · `scripts/` · `.github/`.

## 5. 개발 환경 ([docs/11](docs/11-dev-environment.md))

- **코어(Rust)**: macOS/Windows/Linux 빌드·테스트 → **맥에서 일상 개발 가능**. Windows 전용부는 `#[cfg(windows)]` 격리.
- **앱(WinUI 3)**: **Windows 전용** 빌드·실행(PC/VM/CI). 풀 VS 불요(VS Build Tools+CLI 가능).
- **다른 PC**: clone → `scripts/bootstrap.ps1`(winget) → 빌드. **CI(windows-latest)** 가 신뢰 원천.

## 6. 작업 규약 (이 저장소에서)

- **개발 방식**: **작은 기능 단위(수직 슬라이스)로 순차**, **단위=커밋 1개**(분리 분석),
  **초안→확장 프로토타이핑(incremental)**, main 항상 green. → [docs/15](docs/15-dev-methodology.md).
- **커밋 규약**: Conventional Commits `type(scope): 요약`, 초안/확장 분리 커밋. 단위 백로그는 docs/15 §7.
- **문서 우선**: 결정·설계는 `docs/`에, 의사결정은 `docs/10` + `docs/06`(ADR)에 기록.
- **작업 기록(journal)**: 진행마다 `docs/journal/YYYYMMDD_HHMMSS_<slug>.md` 누적(질문·결정·진행).
- **의존성 정책**: **퍼미시브(MIT/Apache/BSD/ISC/MPL-2.0) 온리**, GPL/AGPL 금지, LGPL 격리.
  CI 라이선스 게이트(`cargo-deny`) 예정. (유료 판매 보호)
- **보안/비공개**: 저장소는 **public** → 비밀(서명 인증서·자격증명·라이선스 키·사업/법무)은 **커밋 금지**.
  공유 방법 → [docs/14-context-sharing.md](docs/14-context-sharing.md).

## 7. 새 세션에서 오리엔테이션 하는 법

1. 이 `CLAUDE.md` + [docs/STATUS.md](docs/STATUS.md) 읽기.
2. 최신 [docs/journal/](docs/journal/) 1~2개로 직전 맥락 확인.
3. 결정은 [docs/10](docs/10-decision-record.md), 요구는 [docs/05](docs/05-requirements.md).

## 8. 다음 단계

1. **리포 스캐폴딩**: `core/`+`app/`+CI+`bootstrap.ps1`+`LICENSE`(PolyForm)+`cargo-deny.toml`+`.gitignore`.
2. **M0 스파이크 3종**: ① 인터롭+10만 노드 가상 트리 60fps ② 교차선택→혼합작업 ③ 유휴 RSS/트림/soak.
3. **M1 1순위 묶음**: 계층 경로 바 · 탭/듀얼 · 플래그십 · 컨텍스트 메뉴 · 퀵 런처.
