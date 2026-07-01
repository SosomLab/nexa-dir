# 19 · 구현 기능 현황 & 검증

> **구현된 기능 단위별 요약 + 검증(테스트) 방법**을 한곳에 모은다.
> ⚠️ **기능을 추가/변경할 때마다 이 문서에 항목을 더한다**(구현 위치·커밋·테스트 절차 포함).
> 빌드/실행/디버깅 절차 전반은 [18](18-build-and-test.md), 작업 경위는 [journal/](journal/), 구조는 [16](16-project-structure.md).

전체 코어 테스트: `cd core && cargo test --workspace` (현재 **9 tests green**, **맥/Windows 공통**) · 앱 빌드/실행: [18](18-build-and-test.md) §2·§6.

> ⚠️ **`dotnet build/run app/Nexa.App` 검증은 Windows(또는 Windows VM)에서만.** 맥에서 실행 시 `NETSDK1100`(EnableWindowsTargeting) → 이어 XamlCompiler 실패 — [11 §6-1](11-dev-environment.md)·[11 §4-4 VM](11-dev-environment.md). 맥에서는 `cargo test`로 코어를 검증한다.

---

## M0 — 기반 (인터롭·VFS)

### F1. 인터롭 왕복 (Rust ↔ C# P/Invoke)
- **무엇**: Rust 코어 함수를 C#(WinUI)에서 호출해 값을 주고받는 경계 검증. DR-1 인터롭이 실제 동작함을 입증.
- **구현 위치**:
  - 코어 C ABI: [core/crates/nexa-interop/src/lib.rs](../core/crates/nexa-interop/src/lib.rs) — `nexa_abi_version()`, `nexa_poc_add(a,b)`
  - C# 바인딩: [app/Nexa.App/NativeInterop.cs](../app/Nexa.App/NativeInterop.cs) (P/Invoke, Cdecl)
  - 표시: [app/Nexa.App/MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) `ShowInteropRoundTrip()`
  - 빌드 통합: [app/Nexa.App/Nexa.App.csproj](../app/Nexa.App/Nexa.App.csproj) (cargo→dll 복사)
- **커밋**: `c41dc41`(Rust 초안) · `af07fee`(C# 확장)
- **테스트**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 코어 단위 | `cargo test -p nexa-interop` | `poc_add_roundtrip` 등 통과 |
  | 헤드리스 dll 왕복 | [18](18-build-and-test.md) §6-3 PowerShell Add-Type | `nexa_poc_add(2,3)` → `5` |
  | 앱 실행(스모크) | `dotnet run --project app/Nexa.App` | 창에 `인터롭 OK — abi=1, nexa_poc_add(2, 3)=5` |

### F2. 로컬 디렉터리 스트리밍 열거
- **무엇**: 폴더 내용을 전체 스캔 대기 없이 **도착하는 대로 점진 산출**(가상화 렌더·인라인 트리의 기반, FR-A1).
- **구현 위치**: [core/crates/nexa-vfs/src/lib.rs](../core/crates/nexa-vfs/src/lib.rs)
  - `Entry { name, kind, size, modified }`
  - `read_dir_entries(path) -> io::Result<impl Iterator<Item = io::Result<Entry>>>` (lazy, 엔트리별 Result로 오류 격리)
- **커밋**: `623da9d`
- **테스트**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 코어 단위 | `cargo test -p nexa-vfs` | 3 tests 통과 |
  | — `read_dir_entries_streams_local` | (임시폴더 파일+하위폴더 열거) | name/kind/size 일치 |
  | — `read_dir_entries_missing_path_errors` | (없는 경로) | `Err` 반환 |

### F3. 인터롭 디렉터리 열거 핸들 API (스트리밍)
- **무엇**: F2의 스트림을 C#에 **핸들 기반**(open→next 반복→close)으로 전달하는 C ABI. 진짜 스트리밍.
- **구현 위치**: [core/crates/nexa-interop/src/lib.rs](../core/crates/nexa-interop/src/lib.rs)
  - `nexa_dir_open(*const c_char) -> *mut DirHandle` (실패 시 널)
  - `nexa_dir_next(*mut DirHandle, *mut NexaEntry) -> c_int` (1=엔트리, 0=끝, -1=널인자)
  - `nexa_dir_close(*mut DirHandle)`
  - `#[repr(C)] NexaEntry { name, kind(0/1/2), size, modified_unix_ms(-1=없음) }` — `name`은 다음 호출 전까지 유효
- **커밋**: `541e887`
- **테스트**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 코어 단위 | `cargo test -p nexa-interop` | `dir_handle_enumerates`·`dir_open_null_and_missing` 통과 |
  | — 열거 | (임시폴더 open→next 루프→close) | 파일명/kind=0/size 일치 |
  | — 방어 | (널 경로·없는 경로) | 핸들 널 |
- **C# 바인딩·UI**: → F4에서 완료.

### F4. 디렉터리 열거 C# 바인딩 & UI 목록 표시
- **무엇**: F3 핸들 API를 C#에서 호출해 폴더 내용을 WinUI 목록에 표시 — **코어 스트리밍 열거가 화면까지 도달**.
- **구현 위치**:
  - C# 바인딩: [app/Nexa.App/NativeInterop.cs](../app/Nexa.App/NativeInterop.cs) — `NexaEntry`(StructLayout), `nexa_dir_open/next/close`, `ReadDir(path)` 래퍼(`name` 즉시 `PtrToStringUTF8` 복사), `DirItem` DTO
  - UI: [app/Nexa.App/MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) ListView(x:Bind) + [.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) `LoadDirectory()`(시작 시 사용자 홈 열거)
  - csproj: `AllowUnsafeBlocks`(WinUI WinRT 제네릭 컬렉션 ABI)
- **커밋**: `(이 단위)`
- **테스트**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 앱 실행 | `dotnet run --project app/Nexa.App` | 창에 홈 폴더 항목 목록(종류/이름/크기) + 헤더 "N개 항목 (코어 스트리밍 열거)" |
  | 빌드+기동 | `dotnet build app/Nexa.App` 후 Nexa.App.exe 기동 | 예외 없이 실행 유지 |
- **후속**: → F5 가상화 렌더 · 경로 입력/네비게이션 · 인라인 트리 펼침([07](07-flagship-tree-multiselect.md)).

### F5. ItemsRepeater 가상화 렌더
- **무엇**: 디렉터리 목록을 `ListView` → **`ScrollViewer` + `ItemsRepeater`**(저수준 가상화)로 전환. 보이는 항목만 realize → 인라인 트리·대량 렌더의 토대([01](01-architecture.md) §4, [07](07-flagship-tree-multiselect.md) §7).
- **구현 위치**:
  - [app/Nexa.App/MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — `ScrollViewer` + `ItemsRepeater`(StackLayout 수직) + 동일 행 템플릿(x:Bind)
  - [app/Nexa.App/MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `DirRepeater.ItemsSource`
- **커밋**: `(이 단위)`
- **테스트**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 앱 실행 | `dotnet run --project app/Nexa.App` | 목록이 ItemsRepeater로 표시, 스크롤 시 가시 항목만 생성 |
  | 빌드+기동 | `dotnet build` 후 Nexa.App.exe | 0/0 · 예외 없이 실행 유지 |
- **후속**: 인라인 트리 행(depth·▶/▼) · 코어 **가시 행(VisibleRow) 평면 스트림** · 10만 노드 성능 벤치(NFR-P1/P2).

---

### F6. 레이아웃 골격 (영역 표시·크기 조절·숨김 토글)
- **무엇**: 화면 영역(메뉴 바·도구 모음·퀵 런처·좌/우 듀얼 패널·하단 도킹·상태바)을 **placeholder로 가시화**하고,
  **GridSplitter로 좌↔우·메인↔터미널 크기 조절**, **상태바 토글로 런처/우패널/터미널 숨김**. 개발 방향을 눈으로 점검(docs/20).
- **구현 위치**:
  - [app/Nexa.App/MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — 7행 그리드, `MenuBar`·도구모음·런처·듀얼 패널(`ctk:GridSplitter`)·터미널·상태바. 좌 패널 콘텐츠에 F4/F5 목록 보존
  - [app/Nexa.App/MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `OnToggleLauncher/RightPanel/Terminal`(splitter·행/열 0 동반)
  - [app/Nexa.App/Nexa.App.csproj](../app/Nexa.App/Nexa.App.csproj) — `CommunityToolkit.WinUI.Controls.Sizers`(GridSplitter, MIT)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 앱 실행 | `dotnet run --project app/Nexa.App` | 영역마다 **뚜렷한 배경색**으로 구별, 좌/우 패널 **50:50**, 좌↔우·상↔하·**하단 좌↔우** 경계 **드래그로 크기 조절**, 상태바 토글로 **런처/우패널/하단/하단분리** |
  | 하단 분리 | "하단 좌/우 분리" 토글 ON(기본) | 하단이 좌(청록)/우(자주) **2개 도킹**(각 패널용 정보·터미널) — 터미널 활성 시 좌·우 2개 |
  | 빌드 | `dotnet build app/Nexa.App` | 0/0(첫 빌드 시 CommunityToolkit 패키지 복원) |
- **비고**: macOS 빌드 불가 → **Windows/VM에서 빌드·실행**([11 §4-4](11-dev-environment.md)). 각 영역은 **placeholder**(기능 미구현) — 방향 점검용. 색상은 초안용(추후 Fluent 테마로 교체).
- **후속**: 영역별 채움 — 네비게이션(폴더 진입·↑) → 경로 바 → 탭 모델 → 우 패널 듀얼 → 인라인 트리.

---

### F7. 좌/우 듀얼 목록 + 하단 도킹 연동 + 메뉴 Fit
- **무엇**: 우 패널도 좌 패널과 동일하게 파일 목록 표시. 하단 도킹 좌/우 분리는 **듀얼(우 패널 표시)일 때만** — 우 패널 숨기면 하단 우도 숨김 + "분리" 토글 비활성(`UpdateBottomDock`). 메뉴 바 세로 축소(Fit).
- **구현 위치**: [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml)(우 패널 목록·MenuBar Height) · [.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs)(`LoadDirectory` 파라미터화, `UpdateBottomDock`)
- **커밋**: `aee82c2`
- **테스트**: `dotnet run` → 좌/우 목록, 우 패널 숨기면 하단 우도 숨김·분리 토글 비활성.

### F8. NexaFileGrid 추출 (재사용 컨트롤 라이브러리)
- **무엇**: 좌/우 목록의 `ScrollViewer`+`ItemsRepeater`를 재사용 컨트롤 **`NexaFileGrid`**(도메인 비종속)로 추출. `Nexa.Controls` 라이브러리 신설. **독립 판매 제품**(ADR-0002 §9, 라이선스 = Nexa Dir 동일 PolyForm).
- **구현 위치**: [app/Nexa.Controls/NexaFileGrid.xaml(.cs)](../app/Nexa.Controls/) · 앱 `ProjectReference` + 좌/우 `<ctrls:NexaFileGrid>`
- **커밋**: `d974b4a`(추출) · `4472ff8`(개명) · `732994d`(제품화/라이선스)
- **테스트**: `dotnet build` 0/0 · 앱 기동 좌/우 `NexaFileGrid` 목록 정상.

### F9. 스플리터 개선 (얇게 + 자석식 스냅)
- **무엇**: GridSplitter 두께 축소(8→4px, 옅은 배경으로 가시화) + **자석식 스냅** — 좌/우 분리선을 ① **창 중앙(50:50)** ② **상↔하 분리선 위치**에 정렬(양방향). 임계 24px 내면 흡착, 벗어나면 해제.
- **구현 위치**:
  - [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — `PanelSplitter`/`TermSplitter`/`BottomSplitter` `Width/Height=4`+배경, 좌 패널·하단 좌 도킹 Border에 `x:Name`+`SizeChanged`
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `OnTopSplitSizeChanged`/`OnBottomSplitSizeChanged`/`SnapTarget`/`ApplySnap`(재진입 가드 `_snapping`, 분리선 위치=좌 열 `ActualWidth`)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 앱 실행 | `dotnet run --project app/Nexa.App` | 스플리터가 **얇고** 옅게 보임 |
  | 중앙 스냅 | 좌/우 경계를 창 중앙 부근으로 드래그 | **50:50에 흡착** |
  | 상↔하 정렬 | 상단 경계를 하단 분리선 X 부근으로(또는 반대) | **두 분리선 X 정렬에 흡착** |
- **후속**: 스냅 위치 설정화 · 스플리터 위치 **설정 파일(JSON) 저장/복원**(백로그).

---

## 구현 순서 (다음 단계 로드맵)

> 트랙 **A(기반) → B(탐색) → C(플래그십) → D(주변)**. 각 단위 ≈ 1커밋(초안→확장). 백로그 근거 [15](15-dev-methodology.md) §7 · 설계 [20](20-ui-layout.md)/[21](21-adr-0002-fileview-control.md)/[23](23-column-system.md).

| 순서 | 단위 | 내용 | 근거 |
| --- | --- | --- | --- |
| **A2** | 컬럼 헤더 + 컬럼 모델 | `NexaFileGrid`에 헤더 행 + `IColumn`/`ICellValueProvider`(이름/상태/크기/날짜) | [23](23-column-system.md) |
| **A3** | 컬럼 리사이즈 | 헤더 경계 드래그로 컬럼 너비 조절 | [23](23-column-system.md) |
| **A4** | 컬럼 동기화 ★요구 | 좌/우 패널 컬럼 크기 동기화(독립/실시간/수동 정책) | [23](23-column-system.md) §3 |
| **A5** | 정렬 | 헤더 클릭 정렬(표시 텍스트와 별개 정렬키) | [23](23-column-system.md) §4 |
| **B1** | 네비게이션 진입 | 더블클릭/Enter 폴더 진입, ↑ 위로 | FR-A3 |
| **B2** | 뒤로/앞으로 + 히스토리 | 탭별 히스토리, 버튼 우클릭 N단계 점프 | FR-A3/B2 |
| **B3** | 경로 바(브레드크럼) | 세그먼트 클릭 이동 + 우클릭 텍스트 주소(복사/붙여넣기/이동) | FR-A2 |
| **C1** | 코어 가시 행 스트림 | `VisibleRow` + `expand`/`collapse` API(코어) | [07](07-flagship-tree-multiselect.md) §3 |
| **C2** | 인라인 트리 펼침 | depth 들여쓰기 + ▶/▼, 지연 로드 | [07](07-flagship-tree-multiselect.md) |
| **C3** | 교차 다중 선택 | `OrderedSet<NodeId>`(부모 무관), Ctrl/Shift/Ctrl+A | [07](07-flagship-tree-multiselect.md) §4 |
| **C4** | 혼합 파일 작업 | 복사/이동/삭제 + 진행률·충돌·**Undo** | FR-D |
| **D** | 주변 | 탭 모델 · 메뉴/툴바/컨텍스트메뉴 실동작 · 일괄 리네임([25](25-bulk-rename.md)) · 미리보기/터미널 패널 · 검색([24](24-search-everything.md), M3) · 뷰 모드(아이콘/컬럼/갤러리) | — |

### 백로그 노트 — 메뉴/툴바 (스타일 vs 크기)
사용자 피드백은 **두 관점**으로 분리해 관리한다.
- **크기(세로 높이)** — ✅ **확정("적당")**: 메뉴 바 28px·툴바 패딩 현 수준 유지. 추가 조정 없음.
- **스타일(모양)** — ⏳ **후속 구현**: 목표 = 참조 스크린샷 수준. ① 메뉴 항목 확장(파일/선택/명령/네트워크/탭/즐겨찾기/표시/구성/도움말 + 드롭다운 실항목) ② 툴바 placeholder 텍스트 제거 → **아이콘 툴바**(Segoe MDL2). 실동작은 트랙 **D**(메뉴/툴바 실동작)에서 배선.

### 백로그 노트 — 설정 시스템 (JSON)
Sublime/VSCode식 **JSON 설정 파일**로 UI 상태를 저장/복원. 변경 시 저장, 실행 시 로드.
- 1차 대상: **스플리터 위치**(좌/우·상/하·하단 좌/우), 패널 표시 토글, **컬럼 너비/정렬**(폴더별).
- 직렬화 = `System.Text.Json`. 위치·스키마·마이그레이션은 별도 설계 문서에서 확정 예정. → 트랙 A 이후/병행.

- **현재 위치**: **A2·A3·A4 완료** — 컬럼 헤더/모델 + **헤더 드래그 리사이즈** + **좌/우 동기화**(공유 컬럼 인스턴스로 헤더·본문·양쪽 패널 동시 반영). → **다음 = A5**(정렬).
  - ※ A4는 "실시간 동기화(공유)" 기본 달성. 독립/수동 정책([23](23-column-system.md) §3)은 후속.
- 각 단위는 Windows/VM에서 빌드·기동 검증 + CI green 확인(맥은 코어만).

## 게이트 (모든 기능 공통, 머지 전)
```bash
cd core
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo deny --manifest-path Cargo.toml check licenses bans advisories   # cargo install cargo-deny 최초 1회
```
CI(`.github/workflows/ci.yml`)가 push/PR마다 동일 게이트 + 앱 빌드 수행([18](18-build-and-test.md) §4).
