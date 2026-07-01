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
- **추가 조정**: 폭 2↔4px 비교 후 **4px 확정**(Min/Max 고정) · 스냅 임계 **20px** · **Alt 드래그 시 스냅 임시 해제**(정밀) · 스플리터 **그립 숨김**(`Foreground=Transparent`) · 좌/우·상/하·하단 **최소 크기**(패널 160·하단높이 80, 숨김 시 MinWidth 0으로 완전 접힘).

### F10. 인라인 폴더 펼침 + Finder 스타일 컬럼 (플래그십 초안)
- **무엇**: 폴더 행의 디스클로저(▶/▼)를 눌러 **자식을 같은 목록에 인라인 삽입/제거**(macOS식 인라인 트리, FR-C 초안). 컬럼을 **이름·수정한 날짜·종류·크기**로 재구성(폴더 우선 정렬, 폴더명 굵게, 파란 폴더 아이콘, 크기 우측정렬). 헤더는 밝은 회색(`HeaderBackground` DP·설정 변경 가능).
- **구현 위치**:
  - [NativeInterop.cs](../app/Nexa.App/NativeInterop.cs) — `DirItem`을 트리 모델로(`Depth`/`FullPath`/`IsExpanded` + 표시용 `ExpandGlyph`/`IconGlyph`/`IconBrush`/`NameWeight`/`ModifiedLabel`/`KindText`), `ReadDir(path, depth)`(폴더 우선+이름 정렬)
  - [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — 4컬럼 공유 인스턴스, 이름 셀(들여쓰기+디스클로저+아이콘+이름) 템플릿
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — 패널별 `ObservableCollection<DirItem>`, `OnToggleExpand`(펼침=자식 삽입/접힘=자손 제거)
  - [NexaFileGrid.xaml(.cs)](../app/Nexa.Controls/) — 전폭 헤더 바 + `HeaderBackground` 의존성 속성
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 앱 실행 | `dotnet run --project app/Nexa.App` | 이름/수정한날짜/종류/크기 컬럼, 폴더 먼저·굵게·파란 아이콘 |
  | 펼침 | 폴더 행 ▶ 클릭 | 자식이 **바로 아래 들여쓰기**로 표시(▼), 다시 클릭하면 접힘 |
  | 헤더색 | — | 본문과 다른 **밝은 회색** 헤더 바 |
- **한계(초안)**: 앱 계층에서 `ReadDir` 재귀 삽입(코어 `VisibleRow` 평면 스트림은 후속 C1). 교차 다중 선택(C3)·지연 가상화·아이콘 실파일형식은 후속.
- **후속**: 헤더 **드래그 재정렬**(본문 컬럼-구동化 필요, [23](23-column-system.md) §6-2) · 컬럼 설정 모달([23](23-column-system.md) §6-1) · 코어 가시행 스트림(C1).

### F11. 파일 선택 (단일·다중·범위) — 플래그십 C3 초안
- **무엇**: 행 클릭 선택 + **Ctrl 토글 다중** + **Shift 범위**(기준점~클릭, 목록 순서). Ctrl+Shift는 기존 선택에 범위 추가. 선택 행 반투명 하이라이트, 상태바 "N개 선택됨". 좌/우 패널 독립 선택.
- **구현 위치**:
  - [NativeInterop.cs](../app/Nexa.App/NativeInterop.cs) — `DirItem.IsSelected` + `RowBackground`(선택=반투명 파랑/비선택=투명)
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `OnRowTapped`(수정자별 분기), 패널별 기준점(`_leftAnchor`/`_rightAnchor`), `IsCtrlDown`/`IsShiftDown`/`KeyDown`
  - [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — 행 루트 전폭 `Background`(하이라이트)+`Tag`+`Tapped`(디스클로저 클릭은 `e.Handled`로 분리)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 단일 | 행 클릭 | 그 행만 파란 하이라이트 |
  | 다중 | Ctrl+클릭 반복 | 개별 토글 누적 |
  | 범위 | 한 행 클릭 후 Shift+다른 행 | 사이 전체 선택, 상태바 "N개 선택됨" |
- **한계(초안)**: **드래그(러버밴드) 선택**은 미포함 — 목록 오버레이+교차 히트테스트가 필요해 `NexaFileGrid`(컨트롤) 내부 단위로 후속.
- **후속**: 러버밴드 드래그 선택 · Shift+화살표 범위 확장 · 혼합 파일 작업(C4).

### F12. 초박형 커스텀 메뉴 바(NexaMenuBar)
- **무엇**: 기본 `MenuBar`/`MenuFlyout` 한계(초박형 불가·둥근 팝업·hover 전환 불가) 우회. **Button 헤더 + 경량 Popup(사각 Border)**. hover 전환(열린 상태서 옆 메뉴 이동 시 자동 오픈), ALT 토글/문자 단축키, ESC·바깥 클릭 닫기.
- **구현 위치**: [NexaMenuBar.xaml(.cs)](../app/Nexa.Controls/) · [NexaMenu.cs](../app/Nexa.Controls/NexaMenu.cs) · [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml)(9개 메뉴)
- **커밋**: `289c911`·`5045e03`·`57147f0`·`87cefbd`
- **테스트**: 파일 클릭→사각 드롭다운, 옆 메뉴로 이동 시 자동 오픈, Alt 토글/Alt+F, Esc 닫기.
- **후속**: 실제 명령 연결(트랙 D).

### F13. 네비게이션(패널/탭별 이동 기록) + 위로
- **무엇**: 패널별 뒤로/앞으로 스택 + 현재 경로, **뒤로·앞으로·위로(상위 폴더)** 버튼 동작 + 활성상태 갱신. 더블클릭 진입도 기록. 네비 버튼 축소(22×22).
- **구현 위치**: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs)(`Nav`/`Navigate`/`GoBack`/`GoForward`/`GoUp`/`UpdateNavButtons`) · [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml)(`NavBtnStyle`)
- **커밋**: `(이 단위)`
- **테스트**: 폴더 더블클릭 후 뒤로/앞으로/위로 동작, 좌/우 독립 기록, 버튼 활성상태.

### F14. Explorer식 선택/호버 + 키보드 이동 + 클릭 지연 수정
- **무엇**: 호버=옅은 파랑, 선택(활성)=연한 파랑+파란 테두리, 선택(비활성/윈도우 비활성)=회색(포커스아웃). 테두리 항상 1px(높이 점프 방지). **키보드 ↑/↓** 단일 선택 이동. **클릭 지연** = `Tapped`+`DoubleTapped` 더블탭 대기 → **`PointerPressed`**(즉시)로 이동.
- **구현 위치**: [NativeInterop.cs](../app/Nexa.App/NativeInterop.cs)(`DirItem` 선택/호버/포커스 브러시) · [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs)(`SetActivePanel`/`RefreshSelectionFocus`/`OnWindowActivated`/`OnGridKeyDown`/`OnRowPointerPressed`) · [NexaFileGrid.xaml.cs](../app/Nexa.Controls/NexaFileGrid.xaml.cs)(`BringIndexIntoView`)
- **커밋**: `(이 단위)`
- **테스트**: 클릭 즉시 선택·행 높이 불변·↑↓ 이동·다른 앱 전환 시 선택 회색.

### F15. 폴더 우선 정렬 옵션화 (설정 준비)
- **무엇**: 그동안 `ReadDir`에 하드코딩돼 있던 "폴더 먼저" 정렬을 **설정 값**(`SortOptions.FoldersFirst`, 기본 `true`)으로 분리. 동작(기본 폴더 우선 + 이름 오름차순)은 그대로지만, 이제 **한 스위치**로 제어 → 나중에 설정 UI/JSON에서 토글. 정렬 로직을 재사용 헬퍼(`SortItems`)로 추출.
- **구현 위치**:
  - [app/Nexa.App/Settings.cs](../app/Nexa.App/Settings.cs) — `SortOptions { FoldersFirst=true }` + `AppSettings.Sort` 인메모리 싱글턴(후속: JSON 로드/저장)
  - [app/Nexa.App/NativeInterop.cs](../app/Nexa.App/NativeInterop.cs) — `ReadDir(path, depth, sort?)`가 `AppSettings.Sort` 참조, `SortItems(items, sort)` 헬퍼(`FoldersFirst` 반영)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 명령 | 기대 |
  | --- | --- | --- |
  | 앱 실행 | `dotnet run --project app/Nexa.App` | 기본값 그대로 **폴더가 파일보다 먼저**, 각 그룹 이름 오름차순 |
  | 빌드 | `dotnet build app/Nexa.App` | 0/0 |
  | 옵션 확인 | `AppSettings.Sort.FoldersFirst=false`(임시) 후 실행 | 폴더/파일 구분 없이 **이름만으로** 정렬 |
- **한계(초안)**: 설정 UI·JSON 영속화 없음(코드 기본값이 유일 원천). 변경 시 열린 목록 **재정렬은 미포함**(다음 로드/펼침부터 반영).
- **후속**: 설정 시스템(JSON) 로드/저장 · **표시(보기) 메뉴 토글**(NexaMenuEntry에 체크·명령 확장, 트랙 D) · 정렬 키/방향(A5).

### F16. UX 수정 — 리사이즈 커서 · 키보드 이동 · 탭 클릭 활성화
- **무엇**: 사용자 피드백 3건 반영.
  1. **컬럼 리사이즈 커서**: 헤더 경계에 마우스를 올리면 일반 화살표 대신 **좌우 화살표(SizeWestEast)** 커서로 바뀌어 리사이즈 위치를 식별. WinUI 3는 `ProtectedCursor`(protected)로만 커서 지정 가능 → 전용 서브클래스 `ColumnResizeGrip`(Grid 기반)로 노출.
  2. **키보드 위/아래 이동 수정**: UserControl 포커스 경로에 의존하지 않도록 **최상위 `RootGrid`에서 `AddHandler(KeyDownEvent, …, handledEventsToo:true)`** 로 방향키 수신 + **활성 패널(`_activeLeft`) 기준** 처리(탭/행 클릭이 활성 패널을 정함). 내부 `ScrollViewer`가 먼저 Handled 처리해도 항상 수신. (1차 시도 = 그리드별 AddHandler는 포커스 문제로 미동작 → RootGrid 방식으로 정정)
  3. **탭 이름 클릭 → 패널 활성화**: 좌/우 **탭 바(Border)** 클릭 시 그 패널을 활성화(`SetActivePanel`)하고 목록에 포커스(키보드 이동 대상 지정).
- **구현 위치**:
  - [app/Nexa.Controls/ColumnResizeGrip.cs](../app/Nexa.Controls/ColumnResizeGrip.cs)(신설) · [NexaFileGrid.xaml](../app/Nexa.Controls/NexaFileGrid.xaml)(핸들을 `ColumnResizeGrip`으로)
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs)(생성자 `AddHandler` 배선 · `OnTabBarTapped`) · [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml)(탭 바 `Tag`/`Tapped`, 그리드 XAML `KeyDown` 제거)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 커서 | 컬럼 헤더 경계(우측 8px)에 마우스 올림 | **좌우 화살표 커서**로 변경 |
  | 키보드 | 행 클릭 후 ↑/↓ | 선택이 한 칸씩 이동 + 화면에 유지(스크롤) |
  | 탭 클릭 | 비활성 패널의 "홈/탭 1/탭 2" 영역 클릭 | 그 패널 활성(선택색 파랑, 반대 패널 회색) |

### F17. 키보드 범위 선택(Shift) + 폴더 펼침/접힘(→/←)
- **무엇**: 키보드 이동 확장.
  - **Shift+↑/↓**: 기준점(anchor) 고정, 캐럿만 이동하며 **범위 다중 선택** 확장(연속). 캐럿(현재 위치)을 anchor와 분리해 관리(`_leftCaret`/`_rightCaret`).
  - **→**: 현재 행이 **폴더면 펼침**(디스클로저 클릭과 동일). **←**: 현재 행이 **폴더면 접힘**. 폴더가 아니면 무시.
  - 펼침/접힘 로직을 `OnToggleExpand`에서 **`SetExpanded(item, expand)`** 로 추출해 클릭·키보드 공용.
- **구현 위치**: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `OnGridKeyDown`(Shift 범위·→/← 분기), `SetExpanded`(공용), `_leftCaret`/`_rightCaret`(캐럿), `OnRowPointerPressed`(클릭 시 캐럿 갱신)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 범위 | 행 클릭 후 **Shift+↓/↑** 반복 | 기준점~현재까지 연속 다중 선택, 상태바 "N개 선택됨" |
  | 펼침 | 폴더 행 선택 후 **→** | 자식이 인라인 펼침(▼) |
  | 접힘 | 펼쳐진 폴더 행에서 **←** | 자식 접힘(▶) |
- **한계**: ← 접힘 시 부모로 이동(트리 상위 점프)은 미포함(현재 행 접힘만). Ctrl+↑/↓(선택 유지 이동)·Ctrl+A는 후속.

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
| **D** | 주변 | **명령 레지스트리 + 커맨드 팔레트**([26](26-command-palette.md), 메뉴/툴바/컨텍스트메뉴/단축키 실동작을 이 위로 통합) · 탭 모델 · 일괄 리네임([25](25-bulk-rename.md)) · 미리보기/터미널 패널 · 검색([24](24-search-everything.md), M3) · 뷰 모드(아이콘/컬럼/갤러리) | — |

### 백로그 노트 — 메뉴/툴바 (스타일 vs 크기)
사용자 피드백은 **두 관점**으로 분리해 관리한다.
- **크기(세로 높이)** — ✅ **확정("적당")**: 메뉴 바 28px·툴바 패딩 현 수준 유지. 추가 조정 없음.
- **스타일(모양)** — ⏳ **후속 구현**: 목표 = 참조 스크린샷 수준. ① 메뉴 항목 확장(파일/선택/명령/네트워크/탭/즐겨찾기/표시/구성/도움말 + 드롭다운 실항목) ② 툴바 placeholder 텍스트 제거 → **아이콘 툴바**(Segoe MDL2). 실동작은 트랙 **D**(메뉴/툴바 실동작)에서 배선.

### 백로그 노트 — 설정 시스템 (JSON)
Sublime/VSCode식 **JSON 설정 파일**로 UI 상태를 저장/복원. 변경 시 저장, 실행 시 로드.
- 1차 대상: **정렬 옵션**(`sort.foldersFirst` = F15 첫 입주자), **스플리터 위치**(좌/우·상/하·하단 좌/우), 패널 표시 토글, **컬럼 너비/정렬**(폴더별).
- 직렬화 = `System.Text.Json`. `AppSettings`([Settings.cs](../app/Nexa.App/Settings.cs)) → `SettingsStore`(로드/저장/변경통지)로 확장. 위치 `%APPDATA%/NexaDir/`.
- **커맨드 팔레트/단축키와 통합**: `settings.json`·`keybindings.json`·`commands.user.json`·`state.json` 4파일 구성·양방향 연동·스키마·마이그레이션 → **[26](26-command-palette.md) §5**에서 확정. → 트랙 A 이후/병행 + 트랙 D.

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
