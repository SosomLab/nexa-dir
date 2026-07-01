# 10 · 통합 결정 기록 (Decision Record)

> 전체 요구사항(05) 기준으로 **개발 방향·기술 요소·디자인 요소를 확정**한 기록.
> 확정일: **2026-06-30**. 이후 변경은 새 ADR/journal로 추적.

---

## 1. 확정된 핵심 결정 (4)

| # | 영역 | 결정 | 근거 |
| --- | --- | --- | --- |
| **DR-1** | 기술 스택 (OD1) | **안 A — Rust 코어 + WinUI 3(C#)** | 평가 86점 1위. 핫패스 네이티브 + 모던 네이티브 UI, 플러그인 호스트(wasmtime/mlua)·ConPTY·오류격리·상주규율 모두 부합. → [06](06-adr-0001-stack.md) Accepted |
| **DR-2** | 디자인 방향 | **프로툴 고밀도 (Path Finder풍)** | 정보 밀도·키보드 우선·전문가 워크플로우. 플래그십(인라인 트리+교차선택)·듀얼/탭과 정합 |
| **DR-3** | 배포 (OD4) | **MSIX + GitHub Releases + winget** (1차) + **포터블(폴더/단일exe) 가능하도록 설계** | 서명 MSIX 자동화. 포터블은 우선순위 아님·배포 시 판단하되 **지금 막지 않도록** 설계 → [12](12-packaging-portable.md) |
| **DR-4** | AI 제공 (OD2) | **보류 — M5 착수 전 별도 ADR** | 프라이버시/비용 정책은 시점에 맞춰 결정 |
| **DR-5** | 라이선스 (OD3) | **소스공개 제한형 — PolyForm Noncommercial**(대안 BSL), 저장소 **공개 예정**(현재 private, 진행 후 public 전환), 개인 무료/상업 유료 | 소스 공개(신뢰) + 유료 판매 보호 양립 → [13](13-licensing.md) |

> **저장소 가시성(보완)**: 현재 **private**. 소스공개(DR-5) 방향에 따라 **어느 정도 진행된 뒤 public 전환 예정**.
> 그때까지도 공개 대비 비밀 커밋 금지 규율을 적용한다(시점 변경은 journal로 추적). [STATUS](STATUS.md)·[14](14-context-sharing.md) 동일 반영.

## 2. 기술 요소 동결 (Tech Stack Freeze)

| 레이어 | 선택 | 비고 |
| --- | --- | --- |
| 코어 언어 | **Rust** (`cdylib`) | VFS·인덱스·프리뷰·ops·ai·pty·plugin 호스트 |
| UI | **WinUI 3 (C#/.NET 8)** | 프로툴 고밀도 테마, 가상화 트리 자체 구현 |
| 인터롭 | C ABI + **csbindgen** | 핸들 기반 표면, 이벤트 스트림 |
| 가상 트리/선택 | 코어 **가시 노드 평면 스트림** + `ItemsRepeater` 가상화 | 플래그십 [07](07-flagship-tree-multiselect.md) |
| 전문 검색 | **tantivy** | 증분 인덱싱, watcher 연동 |
| 파일 감시 | **notify** 크레이트 | |
| 이미지/프리뷰 | `image` 등 + 셸 썸네일 | LRU+디스크 캐시 |
| 원격/클라우드 | `Provider` trait + ssh2/aws-sdk/SMB/WebDAV | 단계적(M4) |
| 터미널 | **ConPTY** 호스팅, 코어 `pty` 모듈 | 임베디드 패널 설계동결/구현후속 |
| 셸 통합 | **C# 계층**에서 IContextMenu/IExplorerCommand/IFileOperation | COM 편의 활용 |
| 플러그인 | **WASM(wasmtime/WIT) 1급 + Python/Node 아웃오브프로세스 RPC** | [09](09-plugin-architecture.md) |
| 패키징 | **MSIX** + signtool, GitHub Actions(windows-latest) | DR-3 |

## 3. 디자인 요소 동결 (Design Freeze) — 프로툴 고밀도

| 항목 | 결정 |
| --- | --- |
| 밀도 | **컴팩트 행** 기본(디테일/목록 우선), 다컬럼 메타데이터 |
| 레이아웃 | 상단 **계층 경로 바** → **퀵 런처 바** → (주소/검색) → 좌/우 듀얼 패널(패널별 멀티라인 탭) → 하단 **터미널 패널(후속)** |
| 기반 | WinUI 3 네이티브 + **고밀도 커스텀 테마**(Fluent 기반, 간격 축소) |
| 테마 | 다크 기본 + 라이트, 강조색, 밀도 토글(컴팩트/표준) |
| 폰트 | **기본 = Segoe UI** — App.xaml 리소스로 지정(암시적 `TextBlock` Style + `ContentControlThemeFontFamily`). 아이콘 = Segoe MDL2 Assets. ⚠️ WinUI는 `Grid`에 `FontFamily` 불가(상속형 부착속성 없음) → 리소스 방식 필수 |
| 인터랙션 | **키보드 우선**(F5/F6, →/←, Ctrl/Shift 교차선택), 명령 팔레트, 단일 액션 레지스트리 |
| 뷰 모드 | 목록/디테일(기본)·타일·그리드, 후속 컬럼(Miller)·Flat View([08](08-competitive-feature-survey.md)) |
| 시각 | 인라인 트리 들여쓰기·삼각형, 교차폴더 다중선택 하이라이트, 상태바(선택/용량/잡) |

## 4. 개발 모델 (Mac 우선 / Windows 필수 분리)

> 상세 절차: [11-dev-environment.md](11-dev-environment.md).

- **`core/` (Rust)** — macOS/Windows/Linux 빌드·테스트. **맥에서 일상 개발 가능**.
  Windows 전용(셸 COM·ConPTY)은 `#[cfg(windows)]`로 격리, 맥에선 trait/stub로 컴파일.
- **`app/` (WinUI 3)** — **Windows에서만 빌드·실행**. UI/통합/패키징은 Windows PC/VM/CI.
- **원칙: OS 비의존 코어를 최대화**(로직은 코어, UI는 얇게) → 개발의 대부분을 맥에서 수행.
- **저장소 기반 재현성**: 다른 Windows PC도 clone → `bootstrap.ps1`로 동일 환경 → 빌드/테스트.
- **CI 신뢰원천**: GitHub Actions(windows-latest)가 매 푸시 빌드·테스트·MSIX 산출 → 맥 단독 세션도 실행 산출물 확보.

## 5. 해소된 Open Decisions

| OD | 상태 |
| --- | --- |
| OD1 스택 | ✅ DR-1 (안 A) |
| OD2 AI | ⏸ 보류 (DR-4, M5 전 ADR) |
| OD3 라이선스 | ✅ DR-5 (소스공개 제한형 PolyForm Noncommercial, 공개 예정/현재 private, 개인무료/상업유료) |
| OD4 패키징 | ✅ DR-3 (MSIX+winget) + 포터블 설계([12](12-packaging-portable.md)) |
| OD5 Windows 빌드 환경 | ✅ [11](11-dev-environment.md) (Windows PC/VM + CI) |

## 6. 다음 단계

1. **개발 환경 구성** ([11](11-dev-environment.md)) — 맥(코어), Windows PC 부트스트랩.
2. **리포 스캐폴딩** — `core/`(Rust 워크스페이스) + `app/`(WinUI 솔루션) + CI + bootstrap 스크립트.
3. **M0 스파이크** — ① 인터롭+10만 노드 가상 트리 60fps ② 교차선택→혼합작업 ③ 유휴 RSS/트림/soak.
4. **M1 1순위 묶음** — 계층 경로 바 · 탭/듀얼 · 플래그십 · 컨텍스트 메뉴 · 퀵 런처.
