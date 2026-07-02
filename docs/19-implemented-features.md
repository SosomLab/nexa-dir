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
  | 앱 실행(스모크) | `dotnet run --project app/Nexa.App` | 창에 `인터롭 OK — abi=2, nexa_poc_add(2, 3)=5` |

### F2. 로컬 디렉터리 스트리밍 열거
- **무엇**: 폴더 내용을 전체 스캔 대기 없이 **도착하는 대로 점진 산출**(가상화 렌더·인라인 트리의 기반, FR-A1).
- **구현 위치**: [core/crates/nexa-vfs/src/lib.rs](../core/crates/nexa-vfs/src/lib.rs)
  - `Entry { name, kind, size, modified, attrs }` (`attrs`=Windows 파일 속성 비트, 비Windows=0 — 열거 시 이미 조회한 메타데이터에서 꺼내 **추가 syscall 0**, F24 숨김 필터의 무료 원천)
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
  - `#[repr(C)] NexaEntry { name, kind(0/1/2), size, modified_unix_ms(-1=없음), attrs }` — `name`은 다음 호출 전까지 유효 (`attrs`는 ABI v2에서 추가, F24)
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
- **F13-1 추가(상위 이동 시 자기 선택)**: **위로(상위 폴더) 이동 후 방금 떠난 폴더(=나)를 상위 목록에서 선택·캐럿·스크롤**.
  `GoUp`이 떠나는 경로 기억 → 이동 후 `SelectByPath`(경로 일치 항목 단일 선택 + 포커스). Up 버튼·Alt+↑ 공통. (탐색기 관례)

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

### F17. 키보드 범위/비연속 선택 + 폴더 펼침/접힘 + 캐럿
- **무엇**: 키보드 이동·선택 확장(Explorer 방식).
  - **↑/↓**: 단일 선택 이동. **Shift+↑/↓**: 기준점(anchor) 고정, **연속 범위 다중 선택** 확장.
  - **Ctrl+↑/↓**: 선택은 그대로 두고 **캐럿(위치)만 이동** — 비연속 다중 선택 모드. **Ctrl+Space**: 캐럿 항목 **선택 토글**(나머지 선택 유지) → 떨어진 항목들 다중 선택. **Space**(단독): 캐럿 항목 **단일 선택**.
  - **→/←**: 현재 행이 **폴더면 펼침/접힘**(디스클로저 클릭과 동일, `SetExpanded` 공용). 폴더 아니면 무시.
  - **캐럿 표시**: 선택되지 않아도 현재 위치를 **얇은 포커스 외곽선**(`DirItem.IsCaret` → `RowBorderBrush`)으로 표시 → Ctrl 이동 위치 식별. 캐럿(현재)은 anchor(범위 고정점)와 분리(`_leftCaret`/`_rightCaret`).
- **구현 위치**: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `OnGridKeyDown`(Shift 범위·Ctrl 이동·Space 토글·→/←), `MoveCaret`/`UpdateSelectionCount`/`SetExpanded`(공용), `OnRowPointerPressed`(클릭 시 캐럿 갱신) · [NativeInterop.cs](../app/Nexa.App/NativeInterop.cs)(`IsCaret`·`CaretBorderBrush`)
- **커밋**: `(이 단위)`
- **테스트(Windows/VM)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 범위 | 행 클릭 후 **Shift+↓/↑** | 기준점~현재 연속 선택 |
  | 비연속 | **Ctrl+↓/↑** 로 이동(외곽선만 이동) 후 **Ctrl+Space** 반복 | 떨어진 여러 항목 선택, 상태바 "N개 선택됨" |
  | 단일 | **Space** | 캐럿 항목만 선택 |
  | 펼침/접힘 | 폴더 행에서 **→ / ←** | 인라인 펼침(▼)/접힘(▶) |
- **한계**: **Ctrl+A**·Home/End·PageUp/Down은 후속.
- **F17-1 추가(←로 부모 이동)**: `←`는 이제 **펼쳐진 폴더면 접기, 접힌 폴더/파일이면 상위(부모) 폴더 행으로 이동**(단일 선택).
  목록 최상위(부모 행 없음, `Depth 0`)면 무동작. 부모 = 목록에서 `Depth`가 1 작은 최근접 상위 행(`ParentIndex`).
  구현: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) `OnGridKeyDown`(Left 분기)·`ParentIndex`.
  테스트(Windows): 펼친 트리에서 자식 행(예 `.vscode`)에서 `←` → 부모(`nexa-dir`)로 이동·선택. 최상위에서 `←` → 변화 없음.

### F18. 펼침 상태 **유지** — 진입/이동에도 동일 (FR-X4 일부)
> 개념: "복원(사후 재생성)"이 아니라 **각 폴더가 자신의 펼침 상태를 유지**한다. (내부적으로는 목록 재구성 시
> 저장된 상태를 재적용하지만, 사용자 관점에선 어느 경로로 오가든 펼침 상태가 **그대로 유지**된다.)
- **무엇**: 폴더 인라인 펼침 상태를 **경로 기준으로 패널별 보유**해, 그 폴더로 **진입/이동(더블클릭·뒤로/앞으로/위로)** 해도
  하위의 열린 폴더들이 **동일하게 유지**된다. 접었다 다시 펼쳐도 하위 상태 유지(sticky).
  - 예: `A`에서 `A2`를 펼쳐 `A21/A22/A23`이 보이고 `A22`를 펼쳐 `A221/A222/A223`이 보이는 상태에서
    **`A2`로 진입** → `A2` 뷰에 `A21/A22(펼침:A221/A222/A223)/A23`가 동일하게 표시.
  - 접힘은 그 폴더만 상태에서 제거(자손 펼침 상태는 보존) → **다시 펼치면 하위까지 유지**.
- **구현 위치**: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs)
  - 패널별 `HashSet<string> _leftExpanded/_rightExpanded`(OrdinalIgnoreCase) — 펼친 폴더 경로 상태 보유
  - `SetExpanded`(상태 추가/제거) → `ExpandInPlace`/`CollapseInPlace`(삽입/제거) + `ApplySavedExpansion`(재귀 적용)
  - `LoadDirectory`가 로드 후 `ApplySavedExpansion` 호출 → 진입/이동해도 하위 펼침 **유지**(헤더 항목수는 직접 자식 기준)
- **커밋**: `(이 단위)`
- **테스트(Windows)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 진입 유지 | 폴더 여러 단계 펼친 뒤 그중 한 폴더 더블클릭 진입 | 진입한 폴더의 하위 펼침 상태가 **동일하게 유지** |
  | 위로 유지 | 위로/뒤로로 상위 복귀 | 이전에 펼쳐둔 하위 상태 그대로 |
  | 접힘 후 재펼침 | 접었다가 다시 펼침 | 접기 전 하위 펼침 상태까지 유지 |
- **한계/후속**: 세션 종료 후 영속화(JSON)는 미포함(인메모리) · 외부 변경(watcher) 반영 · 코어 `VisibleRow` 스트림(C1) 이관 시 통합.

### F19. Alt+↓ 항목 활성화 (폴더 진입 / 파일 실행)
- **무엇**: 캐럿(현재) 항목에서 **Alt+↓** → **폴더/심볼릭은 진입**(더블클릭과 동일, 네비 기록), **파일은 실행**(확장자 연결 프로그램).
  키보드만으로 탐색 흐름을 완결(macOS Cmd+↓ 유사).
- **구현 위치**: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs)
  - `OnGridKeyDown`: `e.Key==Down && IsAltDown()` → `ActivateItem(활성패널, 캐럿항목)`
  - `ActivateItem`: 폴더/심볼릭=`Navigate(record:true)`, 파일=`Launcher.LaunchFileAsync`(실패는 상태바 격리)
- **커밋**: `(이 단위)`
- **테스트(Windows)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 폴더 진입 | 폴더 행에 캐럿 두고 **Alt+↓** | 그 폴더로 진입(더블클릭과 동일, 뒤로 가능) |
  | 파일 실행 | 파일 행에 캐럿 두고 **Alt+↓** | 연결된 기본 프로그램으로 열림 |
- **후속**: Enter로도 활성화(옵션) · 더블클릭 시 파일도 실행(현재는 폴더만).

### F20. 패널별 탭 (멀티라인·고정크기, 더블클릭 추가) — FR-B
- **무엇**: 좌/우 패널에 **실제 탭**. 각 탭 = `PanelTab`(경로·이동기록·펼침상태 **탭별**). 활성 탭이 그 패널의 현재 뷰.
  - **멀티라인·고정크기**: `ItemsRepeater` + `UniformGridLayout`(MinItemWidth 132, 줄바꿈으로 여러 줄, 긴 이름은 `…`).
  - **활성 탭 상단 하이라이트 줄** + 배경으로 구분(커스텀 탭).
  - **`+` 버튼·설명 텍스트 제거** → **탭 영역 더블클릭으로 새 탭 추가**.
  - 탭 클릭 → 그 탭으로 전환(경로/펼침 상태 유지). **바로 하단에 네비 버튼(←/→/↑) + 전체 경로**(기존 네비 바).
- **구현 위치**:
  - [app/Nexa.App/PanelTab.cs](../app/Nexa.App/PanelTab.cs)(신설) — `PanelTab`(INotifyPropertyChanged: Title/IsActive/TabBackground/AccentVisibility)
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — 패널별 `ObservableCollection<PanelTab>`+활성 탭, `_leftNav/_leftExpanded`를 활성 탭 접근자로 전환, `SwitchToTab`/`AddTab`/`OnTabTapped`/`OnTabBarDoubleTapped`, `Navigate`가 탭 Title 갱신
  - [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — 좌/우 탭 바를 `ItemsRepeater`(UniformGridLayout) 탭 스트립으로 교체
- **커밋**: `(이 단위)`
- **테스트(Windows)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 추가 | 탭 영역 더블클릭 | 새 탭 생성·활성(멀티라인으로 쌓임) |
  | 전환 | 다른 탭 클릭 | 그 탭 경로/펼침 상태로 뷰 전환, 상단 하이라이트 이동 |
  | 고정크기 | 긴 폴더명 탭 | 고정폭 + `…` 말줄임 |
- **설정(후속, FR-B4/B5)**: 배치(싱글라인+`<`/`>` ↔ 멀티라인)·크기(이름맞춤 ↔ 고정) **택1 설정** · 탭 닫기(x)·중간클릭 닫기·드래그 재배치·세션 복원.

### F21. Alt+방향키 네비게이션 (위로 / 뒤로 / 앞으로)
- **무엇**: 활성 패널에서 **Alt+↑ = 위로(상위 폴더)**, **Alt+← = 뒤로(이전)**, **Alt+→ = 앞으로(다음)**. (Alt+↓ = 활성화 F19)
  브라우저/탐색기 관례. 탭별 이동 기록(F13/F20)과 결합 → 각 탭의 뒤로/앞으로.
- **구현 위치**: [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) `OnGridKeyDown`의 `IsAltDown()` 분기 → `GoUp`/`GoBack`/`GoForward`(F13 재사용).
- **커밋**: `(이 단위, Alt 네비 별도 커밋)`
- **테스트(Windows)**: 폴더 진입 몇 번 후 **Alt+←/→**로 이전/다음, **Alt+↑**로 상위 폴더. 좌/우 패널·탭 독립.
- **F21-1 수정(빈 폴더)**: `OnGridKeyDown`의 **빈 목록 조기 return**이 Alt 네비까지 막아 **항목 0개 폴더에서 Alt+↑/←/→ 무동작**이던 버그 수정 → Alt 방향키 처리를 **목록 가드 앞**으로 이동(목록 무관). (위로 버튼은 별도 Click 핸들러라 정상이었음)

### F22. 탭 닫기 (Ctrl+W · 탭 더블클릭) + "탭 더블클릭 동작" 설정
- **무엇**:
  - **Ctrl+W** → 활성 패널의 **활성 탭 닫기**(최소 1개 유지, 활성 탭 닫으면 이웃 탭으로 전환).
  - **탭 더블클릭** → 설정된 동작. **`탭 더블클릭 시 동작` 설정** 신설(기본 = **탭 닫기**): ①없음 ②탭 닫기 ③즐겨찾기 등록 ④팝업 메뉴.
    *현재 = 닫기만 구현*(③④는 후속, 상태바 안내). 빈 탭 영역 더블클릭(새 탭 추가, F20)과 구분(탭 위 더블클릭은 소비).
- **구현 위치**:
  - [app/Nexa.App/Settings.cs](../app/Nexa.App/Settings.cs) — `TabDoubleClickAction`(None/Close/Favorite/PopupMenu) + `TabOptions.DoubleClick=Close` + `AppSettings.Tab`
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `CloseTab`(이웃 전환), `OnTabDoubleTapped`(설정 분기), `OnGridKeyDown`의 `Ctrl+W`
  - [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — 탭 아이템 `DoubleTapped="OnTabDoubleTapped"`
- **커밋**: `(이 단위)`
- **테스트(Windows)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | Ctrl+W | 탭 2개↑에서 | 활성 탭 닫힘·이웃 활성 (1개면 유지) |
  | 탭 더블클릭 | 기본 설정 | 그 탭 닫힘 |
  | 빈 영역 더블클릭 | | 새 탭 추가(F20) 유지 |
- **후속**: 설정 UI에서 4택1(FR-B) · 즐겨찾기 탭·팝업 메뉴 동작 · 탭 닫기(x) 버튼·중간클릭 닫기.

### F23. NexaPathBar 계층 경로 바 (α: 브레드크럼 클릭 + 우클릭 편집) — FR-A2
- **무엇**: 탭 하단 네비 바에 **`NexaPathBar`** 배치(전체 경로를 세그먼트로). 설계 [27](27-pathbar-component.md).
  - **세그먼트 클릭 → 이동**(현재/마지막 세그먼트는 무동작·hover 없음, **드라이브 `C:`→`C:\`**), 클릭 가능한 세그먼트만 hover 강조.
  - **우클릭 → 편집 모드**: 전체 경로 텍스트·**전체 선택**·복사/붙여넣기 → **Enter 이동**, **Esc/포커스아웃 → 입력 무시·복귀**.
  - **네비게이션 비종속**: 컨트롤은 `Navigated(path)`만 raise, 호스트가 존재 확인 후 `Navigate`(없으면 상태바 안내·복귀).
- **구현 위치**:
  - [app/Nexa.Controls/NexaPathBar.xaml(.cs)](../app/Nexa.Controls/) — UserControl(α; 후속 템플릿드 전환), `Path` DP·`Navigated` 이벤트, 브레드크럼 `ItemsRepeater`+편집 `TextBox` 2모드
  - [app/Nexa.Controls/PathSegment.cs](../app/Nexa.Controls/PathSegment.cs) — 세그먼트 모델(Prefix/Label/FullPath/IsCurrent)
  - [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) — 좌/우 네비 바 `PathText`(TextBlock) → `NexaPathBar`(PathBarL/R), 버튼|경로 Grid 2열
  - [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) — `LoadDirectory`가 `pathBar.Path` 설정, `OnPathBarNavigated`(존재 확인→`Navigate`)
- **커밋**: `(이 단위)`
- **테스트(Windows)**:
  | 방법 | 동작 | 기대 |
  | --- | --- | --- |
  | 세그먼트 클릭 | 상위 세그먼트(예 `System32`) | 그 경로로 이동, 현재 세그먼트는 무반응 |
  | 드라이브 | `C:` 클릭 | `C:\`로 이동 |
  | 편집 | 경로 바 우클릭 → 붙여넣기 → Enter | 그 경로로 이동. Esc/바깥 클릭 → 원복 |
- **시인성 개선**: 세그먼트/구분자 `\` **간격 축소**(라벨 좌우 패딩 1px)로 fullpath처럼 표시 · **hover 시 배경/글자색 반전**(밝은 배경+어두운 글자)으로 확연히 강조.
- **F23-1 파일 경로 처리**: 편집 제출 경로가 **폴더면 그대로 이동**, **파일이면 그 파일의 상위 폴더로 이동 + 그 파일 선택**. (`OnPathBarNavigated`: `Directory.Exists` / `File.Exists`→`GetDirectoryName`→`Navigate`+`SelectByPath`)
- **후속(β/γ, [27](27-pathbar-component.md))**: 오버플로 `…`·UNC · 형제 `▾` 드롭다운 · 세그먼트 드롭 타깃 · VFS 스킴 Segmenter · 템플릿드 컨트롤화.

### F24. 숨김 파일 보기 · 점(.) 파일 숨기기 (독립 토글, 코어 attrs)

- **무엇**: 표시(S) 메뉴에 **체크형 토글 2개**(독립·동시 설정). ① **숨김 파일 보기**(Windows `FILE_ATTRIBUTE_HIDDEN` 표시 여부, 기본 OFF) · ② **점(.) 파일 숨기기**(리눅스식 dotfile 숨김, 기본 OFF). 기본값은 Windows 탐색기와 동일(숨김 속성 감춤·점 파일 표시).
- **왜 코어에서(성능)**: Windows 디렉터리 열거(`FindNextFile`)가 속성을 결과에 이미 포함 → Rust `DirEntry::metadata()`는 Windows에서 **추가 syscall 없이** 속성 제공. 코어가 이미 크기·시각용으로 `metadata()`를 부르므로 `attrs` 노출은 **비용 0**. C#에서 `File.GetAttributes`로 판정하면 **엔트리당 syscall 1회**(10만 노드=10만 회) → 코어 확정.
- **구현 위치**:
  - 코어: [core/crates/nexa-vfs/src/lib.rs](../core/crates/nexa-vfs/src/lib.rs) `Entry.attrs` + `file_attrs()`(`#[cfg(windows)]` MetadataExt), [core/crates/nexa-interop/src/lib.rs](../core/crates/nexa-interop/src/lib.rs) `NexaEntry.attrs` + `nexa_dir_next` 채움 + **ABI v1→v2**
  - 앱: [app/Nexa.App/Settings.cs](../app/Nexa.App/Settings.cs) `ViewOptions{ShowHiddenFiles, HideDotFiles}`·`AppSettings.View` · [app/Nexa.App/NativeInterop.cs](../app/Nexa.App/NativeInterop.cs) `DirItem.Attrs/IsHidden/IsDotFile`·`ReadDir` 필터(`IsVisible`) · [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) `OnToggleShowHidden/OnToggleHideDotFiles`·`ReloadBothPanels`
  - 메뉴: [app/Nexa.Controls/NexaMenu.cs](../app/Nexa.Controls/NexaMenu.cs) `NexaMenuEntry.IsCheckable/IsChecked/Click` · [NexaMenuBar.xaml.cs](../app/Nexa.Controls/NexaMenuBar.xaml.cs) 체크형 렌더(체크 글리프)·탭 토글 · [MainWindow.xaml](../app/Nexa.App/MainWindow.xaml) 표시(S) 메뉴 엔트리 2개
- **커밋**: `(이 단위)`
- **테스트**:
  | 방법 | 명령/동작 | 기대 |
  | --- | --- | --- |
  | 코어 단위 | `cargo test --workspace` | 9 tests 통과(`abi_version_is_two` 포함) |
  | 앱(Windows) | 표시(S) → "숨김 파일 보기" 체크 | 숨김 속성 파일이 목록에 나타남(체크 표시 토글) |
  | 앱(Windows) | 표시(S) → "점(.) 파일 숨기기" 체크 | `.git`·`.vscode` 등 점 파일/폴더가 사라짐 |
  | 독립성 | 두 토글 조합 | 각각 독립 적용(둘 다 OFF=탐색기와 동일) |
- **후속**: System(0x4) 속성·설정 JSON 영속화(docs/19 설정 시스템) · 토글 상태 초기 동기화(현재 기본 OFF=엔트리 기본값과 일치).

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
