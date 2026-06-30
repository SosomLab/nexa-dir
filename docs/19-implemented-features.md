# 19 · 구현 기능 현황 & 검증

> **구현된 기능 단위별 요약 + 검증(테스트) 방법**을 한곳에 모은다.
> ⚠️ **기능을 추가/변경할 때마다 이 문서에 항목을 더한다**(구현 위치·커밋·테스트 절차 포함).
> 빌드/실행/디버깅 절차 전반은 [18](18-build-and-test.md), 작업 경위는 [journal/](journal/), 구조는 [16](16-project-structure.md).

전체 코어 테스트: `cd core && cargo test --workspace` (현재 **9 tests green**) · 앱 빌드/실행: [18](18-build-and-test.md) §2·§6.

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

## 게이트 (모든 기능 공통, 머지 전)
```bash
cd core
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo deny --manifest-path Cargo.toml check licenses bans advisories   # cargo install cargo-deny 최초 1회
```
CI(`.github/workflows/ci.yml`)가 push/PR마다 동일 게이트 + 앱 빌드 수행([18](18-build-and-test.md) §4).
