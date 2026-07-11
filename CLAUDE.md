# CLAUDE.md — Nexa Dir 프로젝트 컨텍스트 (이식용 메모리)

> 이 파일은 **다른 PC에서 clone 시 즉시 컨텍스트를 공유**하기 위한 휴대용 프로젝트 메모리다.
> Claude Code는 저장소 루트의 `CLAUDE.md`를 자동 로드하므로, 새 세션/새 PC에서도 여기서 출발하면 된다.
> **먼저 읽기:** [docs/STATUS.md](docs/STATUS.md)(현황) → [docs/10-decision-record.md](docs/10-decision-record.md)(결정).

## 1. 이 프로젝트는

**Nexa Dir** = 차세대 **Windows 파일 탐색기**(Windows 기본 탐색기 대체). 네이티브 고성능 + 프로툴 UX.
원격: `SosomLab/nexa-dir`. 현 단계: **M1 후반**(M0 완료·`0.1.0`, M1 진행분 `0.2.0`→`0.3.0`, 배포 자산[포터블 zip·setup.exe] `0.3.1`~`0.3.4` 태그). 코어(트리/선택·성능)·파일 조작 전체(전송엔진·Undo/Redo·**셸 컨텍스트 메뉴**[ADR-0005]+**커스텀 레지스트리**·클립보드·DnD[외부 드롭·러버밴드]) · **탭 UX**(드래그·잠금/고정·우클릭 메뉴) · **하단 패널**(정보·미리보기·**ConPTY 터미널**[위치 정확도·복붙]) · **미리보기 플러그인 SDK**(MIT) · **테마 시스템**(라이트/다크, docs/39) · 앱 아이콘까지 구현·실기 QA 통과. 남은 M1 = **퀵 런처**·B-2 S2/S3·설정 시스템(영속+UI)·설계 계약(트랙 C)·watcher(BUG-006). 현황 → [STATUS](docs/STATUS.md)·[MILESTONES](docs/MILESTONES.md)·[DEVLOG](docs/DEVLOG.md).

- 조직: **SosomLab** (<https://sosomlab.com>) · 상업 라이선스 문의: **kiros33@sosomlab.com**
- 개발자(maintainer): Sangyong Bae · **kiros33@gmail.com**

## 2. 확정 결정 (변경 시 새 ADR/journal)

| # | 영역 | 결정 |
| --- | --- | --- |
| DR-1 | 스택 | **Rust 코어(cdylib) + WinUI 3(C#/.NET8)**, 인터롭 C ABI/csbindgen (ADR-0001 Accepted) |
| DR-2 | 디자인 | **프로툴 고밀도(Path Finder풍)**, 다크 기본, 키보드 우선 |
| DR-3 | 배포 | **MSIX + Releases + winget** + 포터블(폴더/단일exe) 가능 설계 |
| DR-4 | AI | 보류 (M5 전 별도 ADR) |
| DR-5 | 라이선스 | **소스공개 제한형 PolyForm Noncommercial**(대안 BSL), **공개 예정(현재 private, 진행 후 public 전환)**, 개인무료/상업유료 |
| DR-6 | 미리보기 플러그인 SDK | **퍼미시브 MIT `Nexa.Plugins`**(미리보기 계약 분리) + 표준 공급자·샘플 플러그인. 앱 본체 라이선스(DR-5)와 **별개**(SDK는 3자 개발용) → [docs/36](docs/36-plugin-development.md) |

## 3. 핵심 요구 (우선순위)

- ★ **플래그십(M1)**: macOS식 **인라인 폴더 확장** + **폴더 교차 다중 선택** + 혼합 일괄 작업 → [docs/07](docs/07-flagship-tree-multiselect.md)
- **좌/우 듀얼 패널 + 패널별 싱글/멀티라인 탭**(M1), **상단 계층 경로 바(우선)**, **컨텍스트 메뉴**(셸 호스팅+고유)(M1)
- **퀵 런처 바**(M1) + **하단 임베디드 터미널**(ConPTY, 설계동결/구현후속)
- **상주 규율 NFR**: 저메모리·주기정리·무간섭·오류격리
- 후속: 미리보기(M2)·검색(M3)·클라우드/네트워크(M4)·AI(M5)·플러그인(M6, WASM+Python/Node)·내장 zip(향후)
- **라이선스 정품 인증(M7)**: 오프라인 1차+온라인 2차, Ed25519 서명 토큰, 앱엔 공개키만 → [docs/17](docs/17-licensing-activation.md)

## 4. 아키텍처 요약 ([docs/01](docs/01-architecture.md))

- **핫패스는 Rust 코어**(VFS·인덱스 tantivy·프리뷰·ops·pty·plugin 호스트 wasmtime/mlua),
  **UI는 WinUI 3(C#)**. **셸 COM/컨텍스트메뉴는 C# 계층**, 플래그십 트리는 **가시 노드 평면 스트림 + 가상화**.
- 리포 구조(현재): `core/`(Rust: nexa-core/vfs/tree/interop ✅, **ABI v7**) · `app/`(`Nexa.App` WinUI [`Terminal/` ConPTY·VT · `Preview/` 호스트] · `Nexa.Controls` 재사용 컨트롤 · **`Nexa.ViewModels`**(net8.0 순수 로직: `FileOps`·정렬·타입어헤드·경로)+`.Tests`(xUnit) · **`Nexa.Plugins`**(미리보기 SDK, MIT)+`.Samples`) · `docs/`(00~37 + DEVLOG/MILESTONES/BRANCHES/TASKS/BUGS/TODO) · `scripts/` · `.github/`. (예정: `tools/nexa-license-gen`)
- 상세 구조·파일별 목적 → [docs/16](docs/16-project-structure.md). 현황 → [docs/STATUS.md](docs/STATUS.md).

## 5. 개발 환경 ([docs/11](docs/11-dev-environment.md))

- **코어(Rust)**: macOS/Windows/Linux 빌드·테스트 → **맥에서 일상 개발 가능**. Windows 전용부는 `#[cfg(windows)]` 격리.
- **앱(WinUI 3)**: **Windows 전용** 빌드·실행(PC/VM/CI). 풀 VS 불요(VS Build Tools+CLI 가능).
- **앱(WinUI 3)은 맥 빌드 불가**(XAML 컴파일러 Windows 전용) — 맥은 Rust 코어 + 크로스플랫폼 C# 로직만(docs/11 §6-1).
- **다른 PC**: clone → bootstrap(**맥 `bootstrap.sh`/brew**, **Win `bootstrap.ps1`/choco→winget**) → 빌드. **CI(windows-latest)** 가 신뢰 원천.

## 6. 작업 규약 (이 저장소에서)

- **개발 방식**: **작은 기능 단위(수직 슬라이스)로 순차**, **단위=커밋 1개**(분리 분석),
  **초안→확장 프로토타이핑(incremental)**, main 항상 green. → [docs/15](docs/15-dev-methodology.md).
- **커밋 규약**: Conventional Commits `type(scope): 요약`, 초안/확장 분리 커밋. 단위 백로그는 docs/15 §7.
- **문서 우선**: 결정·설계는 `docs/`에, 의사결정은 `docs/10` + `docs/06`(ADR)에 기록.
- **빌드/테스트 SSOT**: 부문별 빌드·테스트 절차는 [docs/18](docs/18-build-and-test.md)이 단일 출처. **빌드/테스트 명령·도구·전제·산출물·OS지원이 바뀌는 변경마다 같은 커밋에서 docs/18 갱신**(신뢰 원천은 CI).
- **WinUI 앱 변경 검증**: 앱(WinUI)은 **맥 빌드 불가** → push 후 **CI(windows) `app` job green 확인 필수**(`gh run watch`). 앱 `TargetFramework`는 참조 UI 패키지(CommunityToolkit 등) 제공 TFM과 정합해야 함(불일치=XAML 컴파일 CS0234). `TargetFramework`(빌드 SDK)≠`TargetPlatformMinVersion`(실행 최소)은 정상. → [docs/18](docs/18-build-and-test.md) §2.
- **작업 기록(2026-07-05~ 규약)**: 기록은 **일자 단위**로만. 하루 상세는 `docs/journal/YYYY-MM-DD.md`(**파일 내부 시간 역순**, 최신 위)에 적고, 두 통합 뷰를 갱신 — 시간순 **[docs/DEVLOG.md](docs/DEVLOG.md)**(그날 요약을 맨 위에 추가) + 목적/기능·마일스톤 **[docs/MILESTONES.md](docs/MILESTONES.md)**. 브랜치 이력 [docs/BRANCHES.md](docs/BRANCHES.md). (과거 `YYYYMMDD_HHMMSS_*` 저널·라운드 워크로그는 통폐합 이전 아카이브.)
  **시각 표기(2026-07-08~)**: 저널의 각 작업 단위 아래 `> ⏱ YYYY-MM-DD HH:MM:SS ~ HH:MM:SS (커밋)` — **git 커밋 시각(KST)** 이 초 단위 원천. 단일 커밋 단위는 종료 시각만, 실제 착수는 첫 커밋 이전(직전 단위 종료 직후)으로 전제.
- **의존성 정책**: **퍼미시브(MIT/Apache/BSD/ISC/MPL-2.0) 온리**, GPL/AGPL 금지, LGPL 격리.
  CI 라이선스 게이트(`cargo-deny`) 예정. (유료 판매 보호)
- **자동화/권한**: `.claude/settings.json`(커밋)에 dev 명령 자동 허용 → 다른 PC도 불필요한 확인 없이 진행.
  파괴적 명령은 `ask` 유지. **사용자 요청 범위 내에서는 확인 없이 자동 진행**이 기본.
- **보안/비공개**: 저장소는 **공개 예정**(현재 private, 어느 정도 진행 후 public 전환) → 공개 대비 지금부터 비밀(서명 인증서·자격증명·라이선스 키·사업/법무)은 **커밋 금지**.
  공유 방법 → [docs/14-context-sharing.md](docs/14-context-sharing.md).

## 7. 새 세션에서 오리엔테이션 하는 법

1. 이 `CLAUDE.md` + [docs/STATUS.md](docs/STATUS.md) 읽기.
2. 직전 맥락 = **[docs/DEVLOG.md](docs/DEVLOG.md)**(시간 역순) 최상단 · 기능/마일스톤 현황 = **[docs/MILESTONES.md](docs/MILESTONES.md)** · 최신 일자 파일 `docs/journal/YYYY-MM-DD.md`.
3. 결정은 [docs/10](docs/10-decision-record.md), 요구는 [docs/05](docs/05-requirements.md).

## 8. 다음 단계 (2026-07-08 현행화)

1. ~~M0~~ ✅(`0.1.0`) · **M1 후반**(진행분 `0.2.0`→`0.3.0`): 경로 바·탭/듀얼(+드래그·잠금/고정)·플래그십(인라인 트리·러버밴드)·파일 조작 전체(전송엔진·**Undo/Redo ✅**·**셸 컨텍스트 메뉴 S1+커스텀 레지스트리/Checksum/경로복사 ✅**·클립보드·DnD 외부드롭)·컬럼(정렬·가로 스크롤)·타입어헤드·하단 패널(정보·미리보기·터미널 ✅정확도/복붙)·플러그인 SDK·**테마 시스템(라이트/다크)**·앱 아이콘·**설정 시스템 PREF-1 ✅**(settings.json 영속+`Ctrl+,` 창, docs/40)·**i18n ✅**(Localizer+외부 `.lang` 파일, docs/42).
2. **남은 M1** → [TODO](docs/TODO.md): **퀵 런처 바**(마지막 미착수 기능) · **B-2 S2/S3**(빈영역 배경 셸 메뉴·폴리시) · B-8(BUG-002 이름변경 오발동) · B-4 잔여(Ctrl+A 등·경로바 드롭).
3. **기반 후속(우선 추천)**: 설정 시스템 후속(PREF-2~7: 테마팩·폰트·밀도·컬럼·단축키·런처·즐겨찾기, docs/40) · **watcher 브로커**(BUG-006, B-12w) · 트랙 C(C-1 에러 모델 → C-2 VFS Provider — M2+ 관문) · 다크 팔레트 정비+기본 모드 결정(DR-2).
4. 중기: **nexa-ops 이관**(B-1, FileOps seam) · 일괄 이름변경(docs/25·UDF docs/41)·사이드바(B-7) · 패키징(MSIX/포터블)+`0.4.0` · 접근성(D-3).
> 외부 파일·설정·언어팩 물리 위치 = [docs/43](docs/43-external-files-and-config.md) · 영속 메커니즘 [docs/34](docs/34-settings-and-session-persistence.md).
