# 28 · 실행 용량(메모리·기동) 최적화 — 할 일

> 실행 용량 문제 분석 결과를 **향후 최적화 할 일**로 정리한다. 상주 규율 NFR과 직결:
> **NFR-M2**(유휴 작업집합 <150MB, 트림 후 <80MB) · **NFR-M3**(주기 정리) · **NFR-M4**(메모리 압박 양보) · **NFR-P5**(콜드스타트 <1s).
> 원칙: **측정 우선 · 비파괴 비교 · 최소 변경**([15](15-dev-methodology.md)).

---

## 0. 대상 구성 (메모리 소비원)

`Nexa.App`(WinUI 3, unpackaged) = **관리(.NET) 힙** + **네이티브**(WindowsAppSDK 런타임 · XAML · **`nexa_interop.dll`**(Rust cdylib) · 셸/썸네일 COM). → 관리·네이티브를 **나눠서** 측정해야 원인 분리.

## 1. 조사·최적화 트랙 (A–D)

> 수집물: `app_final.gcdump`(관리 힙 스냅샷) 확보됨.

| # | 트랙 | 내용 | 성격 | 우선 |
| --- | --- | --- | --- | --- |
| **A** | **ReadyToRun 빌드 비교** | `PublishReadyToRun` 산출로 **실행 시 메모리/기동 비교(비파괴)** — 기존 빌드와 나란히 측정 | 측정 | **1(권장)** |
| **B** | **네이티브 메모리 분해** | Process Explorer/**VMMap**로 **모듈별 private bytes** 스냅샷(자동화 스크립트로 일부 수집 가능) → WinAppSDK/XAML/interop dll 비중 파악 | 측정 | 2 |
| **C** | **코드 소규모 최적화** | 리소스 **지연 로드**(뷰/아이콘/컬럼 등) 예제 패치 등 **최소 변경** 개선안 | 구현 | 3 |
| **D** | **관리 힙 심층 추적** | `app_final.gcdump` 기반 **특정 타입 인스턴스 보유 경로(retention trace)** 분석 → 누수/과다 보유 원인 | 측정 | 4 |

### 트랙별 착수 메모
- **A**: `dotnet publish -c Release -p:PublishReadyToRun=true` (self-contained). **R2R는 JIT↓·기동↓이나 이미지 크기↑**, 유휴 메모리 영향은 **측정으로 확인**(추정 금지). 비파괴 = 별도 산출물로 나란히 실행 비교.
- **B**: 산출 스크립트로 `vmmap /p <pid>` 또는 Process Explorer의 private bytes를 CSV 수집 → 모듈(관리 CLR / WindowsAppSDK / `nexa_interop.dll` / 시스템 dll)별 집계. **유휴/최소화 후** 값도 함께(트림 효과).
- **C**: 후보 — 시작 시 즉시 필요 없는 리소스(패널 콘텐츠·아이콘·컬럼 리소스·프리뷰) **지연 로드**, 정적 브러시/리소스 **공유·재사용**, 초기 컬렉션 용량 최소. **한 패치 = 한 단위**로 전후 측정.
- **D**: gcdump에서 상위 보유 타입(예: `DirItem`/브러시/이미지/이벤트 핸들러)의 **GC 루트까지 경로** 확인 → 불필요 강참조/이벤트 미해제 여부.

## 2. WinUI/.NET/Rust 최적화 레버 (검토 체크리스트)

- **ReadyToRun**(A) · **TieredCompilation/PGO** 기본 유지.
- **NativeAOT**: **WinUI 3 XAML 미지원**(리플렉션/XAML 컴파일) → **배제**.
- **트리밍(Trim)**: WinUI XAML은 리플렉션으로 트리밍 시 위험 → **신중**(부분 트리밍·트리머 힌트 필요, 회귀 리스크). 우선순위 낮음.
- **GC 모드**: 데스크톱 기본 **Workstation GC + Concurrent** 적정(서버 GC는 메모리↑) — 변경은 측정 후.
- **초기 working set 축소**: 지연 로드(C) · **캐시 하드캡+LRU 자니터**(NFR-M3) · **유휴/최소화 시 작업집합 트림**(`EmptyWorkingSet`/`SetProcessWorkingSetSize`, [01](01-architecture.md) §5-1) · 메모리 압박 구독(NFR-M4).
- **네이티브(Rust) 측**: cdylib는 소유권 기반 누수 0 지향 · 불필요 상주 버퍼 반환 · 릴리스 `opt-level=3, lto=thin`(이미 적용).
- **포터블/self-contained**([12](12-packaging-portable.md)): 런타임 포함은 **디스크** 영향이 크고 유휴 메모리 영향은 작음 — 혼동 금지(디스크 용량 vs RSS 구분).

## 3. 측정 방법론 (비파괴·재현)

1. **베이스라인 고정**: 동일 시나리오(기동→유휴 60s→폴더 열기→유휴→최소화). 값 = **작업집합 / private bytes**(유휴·트림 후 각각).
2. **A/B 나란히**: 기존 빌드 vs R2R 빌드를 **같은 시나리오**로 비교(1회성 아닌 수 회 평균).
3. **관리 vs 네이티브 분리**: gcdump(관리) + VMMap(네이티브) 동시.
4. **회귀 게이트(후속)**: 유휴 RSS/트림 목표(NFR-M2)를 **soak 측정 항목**으로 CI/수동 체크(M0 스파이크 계획과 연결).

## 4. 로드맵 / 백로그

- 최적화는 **횡단(NFR) 지속 과제** — 특정 마일스톤 종속 아님. 단, **NFR-M2/P5 검증**과 함께 주기 수행.
- 백로그 [04](04-trends-todo.md) "메모리 풋프린트 예산" 항목에 A–D 연결. 착수 순서 = **A → B → D → C**(측정으로 원인 확정 후 최소 변경 C).
- 결과·수치는 journal에 기록, 확정 최적화는 [01](01-architecture.md) §5-1(리소스 관리)에 반영.
