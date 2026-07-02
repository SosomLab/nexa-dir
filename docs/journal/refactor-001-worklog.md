# refactor/001-audit · 진행 로그 (시간순 · 6하원칙)

> **이 문서는 `refactor/001-audit` 브랜치에서 일어난 변화만** 시간순(YYYY-MM-DD HH:MM:SS, KST)으로 기록한다.
> 각 항목은 **6하원칙**(누가/언제/무엇을/왜/어디서/어떻게)으로, **요청 → 진행 방향 → 목표 → 기능 → 세부 구현 → 파일:줄** 순서로 남긴다.
> 진행하는 틈틈이 **맨 끝에 append**한다(새 커밋마다 항목 추가). 시각은 커밋 committer date 기준.

## 브랜치 개요

- **브랜치**: `refactor/001-audit` (일련번호식 — 다음 라운드는 `refactor/002-…`)
- **분기 기준**: `6e817345` (main, 2026-07-02 12:16:24, CI green)
- **목적/요청**: 사용자 — “현재 소스 기반 새 브랜치에서 **소스 점검·중복 코드 제거 리팩토링**. 잘되면 나중에 main 병합. 아울러 **지금까지 개발이 설계 방향에 맞는지 전체(문서·소스·진행) 정합성 점검**.”
- **선택된 진행 방향**(사용자 결정): 감사 후 **아키텍처 정공법 — 코어 `VisibleRow`(C1)** 트랙 착수.

## 진행 ↔ 커밋 계층 매핑 (요약)

```
refactor/001-audit  (분기: 6e81734)
├─ E1 통합 감사 진단 ............................. 41bb9e2  (2026-07-02 13:19:52)
│    └ 산출: docs/journal/20260702_130615_refactor-001-audit.md
├─ E2 C1 설계 결정 (ADR-0004) ................... c38b88f  (2026-07-02 13:31:01)
│    └ 산출: docs/29-adr-0004-core-tree-model.md
├─ E3 C1 슬라이스 1 구현 (nexa-tree 코어 모델) ... 1964dc8  (2026-07-02 13:31:17)
│    └ 산출: core/crates/nexa-tree/** · core/Cargo.toml · docs/19(F26)·STATUS
├─ E4 진행 로그(이 문서) ......................... 89c0690  (2026-07-02 13:34:45)
├─ E5 C1 슬라이스 2 (트리 C ABI + ABI v3) ........ 00966af  (2026-07-02 13:55:12)
│    └ 산출: core/crates/nexa-interop/** · docs/18·19(F27)·STATUS
├─ E6 C1 슬라이스 3a (호스트 ABI 안전 계층) ...... 30d8b16  (2026-07-02 14:04:34)
│    └ 산출: nexa_entry_size + VerifyAbi() · docs/19(F28)·STATUS
├─ E6.1 CI 픽스: deny.toml에 nexa-tree 예외 ...... 284c479  (2026-07-02 15:03:01)
│    └ PR #1(draft) 열고 push → CI 4-job green(core×2·deny·app)
├─ E7 C1 슬라이스 3b-1 (트리 구조체 미러 + 가드) . ee79888  (2026-07-02 15:28:35)
│    └ 산출: NexaRow/NexaRange 미러 + row/range_size + CheckLayout · docs/19(F28)
├─ E8 C1 슬라이스 3b-2 착수 (관리형 트리 클라이언트) 2435b5f  (2026-07-02 15:45:30)
│    └ 산출: nexa_tree_* P/Invoke + TreeOpen/GetRow/Expand/Select… + TreeRow/TreeRange
├─ E8.1 nexa_tree_row_path + TreeRowPath (배관 완료) . 1e53e6e  (2026-07-02 15:53:20)
│    └ 산출: 행 경로 ABI(아이콘/네비 전제) · 코어 17 tests green
├─ E9 코어 가시성 필터(F24 이관) ................. cbccea7  (2026-07-02 16:03:48)
│    └ nexa-tree open_filtered + ABI open(filter) · 코어 18 tests
├─ E10 VirtualTreeCollection 컴포넌트(미배선) ..... b191c69  (2026-07-02 16:17:50)
│    └ 가상화 소비 컬렉션 + DirItem.Id · build only
├─ (docs) 용어집(30) ............................. (인터롭/배관 한영)
├─ E11a VirtualTreeCollection 캐럿·포커스·경로조회 . 86e3ab1  (2026-07-02 16:49:07)
├─ E11b MainWindow 코어 트리 가상화 배선(−250줄) .. d818675  (2026-07-02 17:14:35)  ★실기 QA 필요
└─ E12~ 잔여 C# 정리(3b-3)·성능(4) ............... 예정
```

> 📦 **배관 완료 지점**: 코어 모델 → C ABI(v3, row_path 포함) → 로드 검사·레이아웃 가드 → 관리형 클라이언트(`TreeRow`/`TreeRange`/`TreeRowPath`)까지 전부 CI-green. 남은 것은 **MainWindow 소비 배선**(가상화 컬렉션 + 펼침/선택 위임)뿐 — WinUI 런타임 검증 필요.

> ✅ **CI 검증 완료(PR #1, draft)**: 슬라이스 1·2·3a가 `refactor/001-audit`에서 core(win/mac)·cargo-deny·**WinUI app** 전 job green. 앱 ABI 검사(F28)까지 CI 통과.

---

## E1 · 2026-07-02 13:19:52 · 통합 감사 진단 → `41bb9e2`

- **누가(Who)**: 사용자 요청 → Claude(3개 병렬 감사 에이전트) 수행·통합.
- **왜/요청(Why)**: “개발이 설계 방향에 맞는지, 부족·오류가 없는지 문서·소스·진행을 통합 점검.”
- **목표(Goal)**: 리팩토링 착수 전 **정합성 베이스라인** 확보 — 무엇을 먼저 고쳐야 하는지 우선순위화.
- **무엇을(What)**: 앱코드/설계정합/코어·빌드 3축 감사 → 통합 리포트 1건.
- **어떻게(How)**: `general-purpose` 에이전트 3개 병렬 → 각 findings를 심각도별 통합, 수렴 항목 교차검증.
- **어디서(Where)**: `docs/journal/20260702_130615_refactor-001-audit.md` (신규).
- **핵심 발견(결과)**:
  - **A1** 플래그십 트리/선택이 앱(C#) 코드비하인드에 **O(n)** 축적 → NFR(10만/60fps) 위배(`MainWindow.xaml.cs`).
  - **A2** `Nexa.ViewModels` 부재 — 로직 전부 1124줄 코드비하인드(맥 테스트 불가).
  - **A3** ABI 버전 **표시만·미검사**(v1→v2 범프가 사실상 무의미), 구조체 레이아웃 가드·csbindgen 없음.
  - B: FFI 패닉·CI P/Invoke 미검증·attrs/심링크 무테스트. C: `bool left` 이중화·좌우 XAML 복붙. D: 문서 스테일(STATUS 721줄 vs 실제 1124).

## E2 · 2026-07-02 13:31:01 · C1 설계 결정 (ADR-0004) → `c38b88f`

- **누가(Who)**: 사용자 선택(“아키텍처 정공법 — 코어 VisibleRow C1”) → Claude 설계.
- **왜(Why)**: 감사 A1/A2를 근본 해결 — 트리/선택을 코어로 이관해 성능·이식·테스트성 확보.
- **목표(Goal)**: 구현 전 **결정·크레이트 구조·ABI·슬라이스 계획**을 문서로 확정(문서 우선 규약).
- **무엇을(What)**: ADR-0004 — 신규 크레이트 `nexa-tree`에 **가시 노드 평면 스트림(VisibleRow)** + `OrderedSet<NodeId>` 선택 채택.
- **어떻게/세부(How)**: 핵심 타입(`Node`/`VisibleRow`/`RangeChange`/`Tree`) 스케치, 지연로딩·diff 규약, ABI v3 함수 스케치, **4-슬라이스 계획**(모델→ABI→앱→성능), 범위 밖(watcher/심링크/폴더흡수) 명시, 대안 기각(‘C# 유지’·‘vfs 병합’).
- **어디서(Where)**: `docs/29-adr-0004-core-tree-model.md` (신규).

## E3 · 2026-07-02 13:31:17 · C1 슬라이스 1 — `nexa-tree` 코어 모델 → `1964dc8`

- **누가(Who)**: Claude 구현(초안), 맥 빌드 가능한 순수 로직.
- **왜(Why)**: ADR-0004 슬라이스 1 — UI·ABI 미접촉의 **테스트 가능한 코어 모델**부터.
- **목표(Goal)**: `cargo test`/`fmt`/`clippy -D warnings` 통과하는 트리/선택 모델 + 단위테스트.
- **무엇을(What)**: `nexa-tree` 크레이트 신설(워크스페이스 멤버 등록) — 인라인 트리 + 교차 선택.
- **세부 기능 구현 · 파일:줄** (`core/crates/nexa-tree/src/lib.rs`):
  - 타입: `NodeId=u64`(:17) · `Node`(:20, arena) · `VisibleRow`(:45) · `RangeChange`(:60, `NONE` :68) · `SelectMode`(:77) · `Tree`(:86).
  - 열기/열거: `Tree::open`(:99) → `enumerate`(:130, `nexa-vfs` 스트리밍) → `sort_ids`(:162, 폴더우선+이름).
  - 가시 스트림: `visible_len`(:175)·`is_empty`(:180)·`row`(:185)·`visible_index`(:202)·`collect_subtree`(:207, DFS).
  - 펼침/접힘: `expand`(:221, 지연열거+하위 펼침복원+splice diff)·`collapse`(:251, `depth>base` 연속구간 drain)·`is_expanded`(:280).
  - 선택(OrderedSet): `select`(:287, Single/Toggle)·`select_range`(:305, 가시범위)·`select_all_visible`(:323)·`clear_selection`(:333)·`add_sel/remove_sel`(:338/:344)·`is_selected`(:353)·`selected_ids`(:358)·`selection_count`(:363)·`selected_paths`(:368, 혼합 부모)·`anchor`(:376)·`node_path`(:381).
  - 유틸: `to_unix_ms`(SystemTime→ms).
- **어떻게/검증(How)**: 임시폴더 픽스처 기반 **7 tests**(:393~) — 상단정렬·펼침/접힘 왕복·중첩 재펼침 복원·파일/중복 펼침 no-op·교차폴더 선택 순서·범위/전체 선택·없는 경로 오류. **코어 전체 16 tests green**, fmt·clippy 통과.
- **어디서(Where)**: `core/crates/nexa-tree/{Cargo.toml,src/lib.rs}`(신규) · `core/Cargo.toml`(멤버·의존) · `core/Cargo.lock` · 문서 `docs/19`(F26)·`docs/STATUS.md`(코어 테스트 9→16).

## E4 · (이 커밋) · 진행 로그 문서화

- **누가/왜(Who/Why)**: 사용자 요청 — “진행 틈틈이 브랜치 변화를 시간순 6하원칙으로 상세 기록 + 진행↔커밋 계층 정리.”
- **무엇/어디서(What/Where)**: 이 문서(`docs/journal/refactor-001-worklog.md`) 신설 — 위 매핑·상세 로그. 이후 슬라이스마다 append.

## E5 · 2026-07-02 13:55:12 · C1 슬라이스 2 — 트리 C ABI + ABI v3 → `00966af`

- **누가(Who)**: Claude 구현.
- **왜(Why)**: ADR-0004 슬라이스 2 — 코어 트리/선택(F26)을 C# 호스트가 P/Invoke로 쓰도록 C ABI 표면 노출.
- **목표(Goal)**: 핸들 기반 트리 ABI + ABI 버전 v3 + 라운드트립 테스트(fmt/clippy 통과).
- **무엇을(What)**: `nexa-interop`에 `nexa_tree_*` 함수군 + `NexaRow`/`NexaRange` 구조체 + ABI v2→v3.
- **세부 기능 구현 · 파일:줄** (`core/crates/nexa-interop/src/lib.rs`):
  - `kind_code`(:16, `FileKind`→u32) — `nexa_dir_next`(:97)와 매핑 통일. ABI 버전 `3`(:27).
  - `TreeHandle`(:129, 트리+최근 CString 보관) · `NexaRow`(:138, `VisibleRow` 미러, 8→4→1바이트) · `NexaRange`(:152).
  - `nexa_tree_open`(:164)·`_close`(:187)·`_visible_len`(:198)·`_row`(:211, name 수명 보관)·`_expand`(:243)·`_collapse`(:263)·`write_range`(내부).
  - 선택: `_select`(mode 0/1)·`_select_range`·`_select_all`·`_clear_selection`·`_is_selected`·`_selected_len`·`_selected_path`(경로 수명 보관).
  - 의존: `nexa-interop/Cargo.toml`에 `nexa-tree` 추가.
- **어떻게/검증(How)**: `tree_abi_open_expand_select_collapse` 테스트(열기·펼침 diff `(1,0,1)`·교차 선택·선택 경로 `ends_with(child.txt)`·접힘 `(1,1,0)`·경계/널). `nexa-interop` **5 tests**, 코어 전체 **17 green**, fmt·clippy 통과. `abi_version_is_two`→`_three`.
- **어디서(Where)**: `core/crates/nexa-interop/{src/lib.rs,Cargo.toml}` · `core/Cargo.lock` · `docs/18`·`docs/19`(F27)·`docs/STATUS`(abi 표기 2→3, 테스트 16→17).

## E6 · 2026-07-02 14:04:34 · C1 슬라이스 3a — 호스트 ABI 안전 계층 → `30d8b16`

- **누가/왜(Who/Why)**: Claude 구현. 감사 A2/A3 정정 — `nexa_abi_version()`을 **표시만** 하던 것을 실제 게이트로.
- **목표(Goal)**: 구형/신형 dll이 조용히 로드돼 구조체가 오정렬되는 것을 시작 시 차단(3b 트리 재배선의 안전 전제).
- **무엇/세부(What) · 파일:줄**:
  - 코어: `nexa_entry_size()`([nexa-interop/src/lib.rs](../../core/crates/nexa-interop/src/lib.rs):31, `size_of::<NexaEntry>()`).
  - 앱: `ExpectedAbi=3` + `VerifyAbi()`([NativeInterop.cs](../../app/Nexa.App/NativeInterop.cs)) — ① 버전==3 ② `Marshal.SizeOf<NexaEntry>()`==코어 크기, 불일치 시 `InvalidOperationException`.
  - 앱: `ShowInteropRoundTrip`([MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs):85)이 `VerifyAbi()` 선행 → 불일치 시 `인터롭 실패:` 격리.
- **어떻게/검증(How)**: 코어 fmt/clippy/test 통과(17), 앱 로컬 `dotnet build` 0 err. **WinUI라 CI(app)로 최종 검증**(브랜치 CI는 push만으론 미실행 → PR 필요).

## E7 · 2026-07-02 15:28:35 · C1 슬라이스 3b-1 — 트리 구조체 미러 + 전체 크기 가드 → `ee79888`

- **누가/왜(Who/Why)**: Claude. UI 재배선(3b-2/3) 전에 ABI 안전 계층을 완성 — 트리 구조체까지 레이아웃 가드.
- **무엇/세부(What) · 파일:줄**:
  - 코어: `nexa_row_size()`·`nexa_range_size()` export([nexa-interop/src/lib.rs](../../core/crates/nexa-interop/src/lib.rs)).
  - 앱: 관리형 `NexaRow`/`NexaRange` 미러 struct(8→4→1바이트) + `nexa_row_size`/`nexa_range_size` P/Invoke + `VerifyAbi`가 `CheckLayout` 헬퍼로 3개 구조체 크기 모두 대조([NativeInterop.cs](../../app/Nexa.App/NativeInterop.cs)).
- **검증(How)**: 코어 fmt/clippy/test 17 green, 앱 로컬 build **0 warning**(CS0649 없음). PR CI로 최종.

## E8 · 2026-07-02 15:45:30 · C1 슬라이스 3b-2 착수 — 관리형 트리 클라이언트 → `2435b5f`

- **누가/왜(Who/Why)**: Claude. 전면 가상화 재배선의 토대 — 마샬을 은닉한 관리형 API를 먼저.
- **무엇/세부(What) · 파일:줄** ([NativeInterop.cs](../../app/Nexa.App/NativeInterop.cs)):
  - `nexa_tree_*` 13개 P/Invoke(private) + 관리형 래퍼(`TreeOpen/TreeClose/TreeVisibleLen/TreeGetRow/TreeExpand/TreeCollapse/TreeSelect/TreeSelectRange/TreeSelectAll/TreeClearSelection/TreeIsSelected/TreeSelectedLen/TreeSelectedPath`).
  - 관리형 record `TreeRow`(VisibleRow)·`TreeRange`(RangeChange). `NexaRow.Name`은 즉시 `PtrToStringUTF8` 복사.
- **검증(How)**: 앱 로컬 build 0 warning/0 err. PR CI로.

## E9 · 2026-07-02 16:03:48 · 코어 가시성 필터(F24 이관) → `cbccea7`

- **왜**: 코어 트리가 `ReadDir`을 대체할 때 F24(숨김/점 파일) 회귀 방지 — 필터를 코어로(감사 M2).
- **무엇**: `nexa-tree` `Filter{show_hidden,show_dotfiles}` + `Tree::open_filtered`(enumerate에서 걸러진 항목 미생성) · ABI `nexa_tree_open(path,show_hidden,show_dotfiles)` · 관리형 `TreeOpen(path,showHidden,showDotFiles)`. 테스트 `open_filtered_excludes_dotfiles`(코어 18 green). **QA 불필요(코어 테스트).**

## E10 · 2026-07-02 16:17:50 · VirtualTreeCollection 컴포넌트(미배선) → `b191c69`

- **왜**: MainWindow 배선의 핵심 컴포넌트를 먼저 독립 구현·리뷰(미배선=런타임 무영향).
- **무엇 · 파일** ([VirtualTreeCollection.cs](../../app/Nexa.App/VirtualTreeCollection.cs)): 가시 인덱스만 지연 생성·캐시, 펼침/접힘/선택을 코어 위임. `IList+IReadOnlyList<DirItem>+INotifyCollectionChanged+IDisposable`. `Open/this[i]/ToggleExpand/Select/SelectRange/SelectAll/ClearSelection/SelectedPaths`. 구조변경=Reset 통지. `DirItem.Id`(NodeId) 추가. **QA 불필요(미배선, build only).**

## E11 · C1 3b-2 배선 (E11a 컴포넌트 강화 → E11b MainWindow 교체)

- **E11a `86e3ab1`**: `VirtualTreeCollection`에 캐럿(`SetCaret`/`CaretIndex`/`CaretItem`)·패널포커스(`SetPanelFocused`)·경로조회(`IndexOfPath`)·`RowBuilt` 콜백 추가(전이 행에 얹는 UI 상태). 미배선.
- **E11b `d818675` (★실기 QA 필요)**: `MainWindow` 배선 — 목록 필드를 `VirtualTreeCollection`로 교체, `LoadDirectory`→`Open`(필터 코어), `OnToggleExpand`→`ToggleExpand`, 클릭/키보드 선택→`Select/SelectRange`(코어 OrderedSet), 캐럿→`SetCaret`, 포커스색→`SetPanelFocused`, 아이콘→`RowBuilt`. **제거**: `SetExpanded`/`ExpandInPlace`/`CollapseInPlace`/`ApplySavedExpansion`/`ExpandedSet`/`NormPath`/`MoveCaret`/구 `ParentIndex` + anchor/caret 필드 → **−250줄**. app build 0 warn/err(빌드만).

### ★ E11b 실기 QA 체크리스트 (앱 실행 후 확인)

1. 표시: 폴더 목록 정상(이름/폴더우선/날짜/크기/**아이콘 지연 로드**).
2. 필터: 표시(S) "숨김 파일 보기"·"점(.) 파일 보기" 해제 시 각각 사라짐(F24).
3. 네비: 더블클릭 진입 / 상위 / 뒤로·앞으로(버튼·마우스 XButton) / 경로바 / **←로 부모 이동**.
4. 인라인 펼침: ▶ 클릭·→키 펼침, ▼·←키 접힘, 중첩 펼침 후 접었다 펴기 복원.
5. 선택: 단일 / Ctrl 비연속 / Shift 범위 / **교차 폴더** / (키보드 Space·Shift+↑↓·Ctrl+↑↓).
6. 키보드: ↑↓ 이동 · 캐럿 외곽선 표시 · Alt+↓ 진입.
7. 탭·듀얼: 좌/우 독립 · 활성 패널 선택색(파랑) vs 비활성(회색) · 탭 전환 유지.
8. 안정성: 큰 폴더·빠른 펼침/접힘 반복 시 크래시·멈춤 없음.

> ⚠️ 알려진 러프 엣지(발견 시 알려주세요 → 후속 수정): 펼침/접힘은 현재 **Reset 통지**라 스크롤 위치·미세 깜빡임 가능(성능 슬라이스4에서 범위 diff로 개선). 펼침 후 캐럿/스크롤 유지 정도.

## ★ E11b 실기 QA 결과 (2026-07-02) — 회귀/개선 TODO

사용자 실기 확인. 3건 접수:

1. **[회귀 · 머지 전 복원 필요] F18 펼침 상태 유지(진입/이동)**
   - 증상: 폴더를 펼쳐둔 뒤 **다른 폴더로 이동/진입하면 이전 펼침이 사라지고 평면 목록만** 표시. (한 뷰 내 펼침/접힘·재펼침 복원은 정상.)
   - 원인: E11b가 `_leftExpanded/_rightExpanded`(경로셋) + `ApplySavedExpansion` 제거. 트리 핸들이 `Open`마다 새로 생성돼 펼침 상태 유실.
   - 기록 대조: **[F18](../19-implemented-features.md) "한계\후속"에 "C1 이관 시 통합"으로 이미 예고된 항목** — 예견된 회귀.
   - **결정: TODO(이 브랜치 main 머지 전 복원)**. 깔끔한 복원안 = `PanelTab.Expanded`(경로셋) 유지 + `Open` 후 **얕은→깊은 순 재펼침**(컬렉션 `ExpandPaths` 배치 API로 Reset 1회) 또는 코어 `nexa_tree_*`에 경로 펼침 지원 추가. → **E12에서 별도 슬라이스로**.
2. **[신규 · 설정] 헤더 정보란 토글**
   - 요청: 그리드 상단 `현재경로 — N개 항목`(`DirHeader`) 표시를 **설정에서 on/off**.
   - → **설정 화면 백로그**([26 §8](../26-command-palette.md)) + `ViewOptions.ShowHeaderInfo`(예정). 지금은 문서화만.
3. **[성능 · 개선 필요]** 대용량 폴더(예: `C:\Windows\System32` 4880개) **느림·스크롤 비자연**.
   - 원인 추정: 펼침/접힘 **Reset 통지**(전체 재실체화) + `visible_index`/`IndexOf` **선형 O(n)** + `row`/`row_path` 매 호출 마샬.
   - → **슬라이스 4(성능)**: 범위 diff 통지(Reset→Insert/Remove) · 행 인덱스↔노드 **O(log n)**(부모별 자식수 캐시) · 행 캐시/일괄 조회. (AC5 벤치와 함께)
4. **[성능 · 개선 필요]** 파일 많은 디렉터리 여러 개를 탭으로 열고 **탭 전환 시 클릭 반응이 살짝 지연**되는 느낌.
   - 원인 추정: 탭 전환=`SwitchToTab`→`Navigate`→`LoadDirectory`→`Open`(새 트리 핸들) + **Reset** → `ItemsRepeater` 전체 재실체화가 **UI 스레드 동기** 처리. 각 행 실체화가 `row`+`row_path` 2회 P/Invoke + 아이콘 로드 유발.
   - → **슬라이스 4**: 탭별 트리 핸들 **캐시/보존**(전환 시 재-Open 회피) · 초기 창만 실체화 · 행 조회 일괄화 · 아이콘 로드 스로틀. (#3과 동일 성능 트랙)

## E12 · C1 — F18 펼침 상태 유지 재구현 (QA #1 복원) → `(이 커밋)` ★실기 QA

- **왜**: E11b 회귀(진입/이동 시 펼침 유실) 복원 — 머지 전 필수.
- **무엇 · 파일**:
  - [VirtualTreeCollection.cs](../../app/Nexa.App/VirtualTreeCollection.cs) `ExpandPaths(paths)` — `Open` 후 **얕은→깊은 순 배치 재펼침**(코어 직접 질의, 부모 먼저 → 자식 가시화, Reset 1회).
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs) `ToggleExpandRow`(디스클로저·키보드 →/← 공용) — 코어 토글 후 **탭별 경로셋**(`PanelTab.Expanded`) add/remove. `LoadDirectory`가 `Open` 후 `ExpandPaths(tab.Expanded)` 호출. 헤더 항목수는 재펼침 전 직접 자식 수 유지.
- **검증(How)**: app build 0 warn/err. **★실기 QA 통과(2026-07-02)** — (a) C:\에서 Oracle 하위 다단계 펼침 → **Oracle 진입** 시 펼침 유지 + 진입 뷰에서 network 하위 확장 정상, (b) **확장 후 위로(상위) 이동** 시에도 펼침 상태 유지(사용자 스크린샷 2건). 진입·위로 모두 확인 → **F18 회귀 해소**.

## E13 · C1 — 네비게이션 스크롤 위치 수정 (QA #5/#6) → `(이 커밋)` ★실기 QA

- **증상**(사용자): (5) 폴더 **진입 시 맨 위**로 안 가고 이전 폴더의 스크롤 비율 위치가 보임. (6) **상위 이동** 시 이전 폴더가 선택은 되나 **화면 밖**(스크롤 필요).
- **원인 분석**: C1이 같은 `VirtualTreeCollection`을 **Reset**으로 재사용 → (5) `ScrollViewer` 세로 오프셋이 이전 폴더 값 그대로 잔존. (6) `BringIndexIntoView`가 `Repeater.TryGetElement`(미실체화=null)라 **오프스크린 대상엔 무동작**.
- **수정**:
  - [NexaFileGrid](../../app/Nexa.Controls/NexaFileGrid.xaml.cs): `ScrollToTop()`(오프셋 0) + `ScrollIndexIntoView(index, ratio)`(`GetOrCreateElement`+`UpdateLayout`로 **강제 실체화** 후 `StartBringIntoView(VerticalAlignmentRatio=ratio)`). 본문 ScrollViewer `x:Name=BodyScroll`.
  - [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs): `LoadDirectory`가 로드 후 `ScrollToTop()`(진입=맨 위) · `SelectByPath`가 `ScrollIndexIntoView(i, ViewOptions.UpNavTargetAlign)`(상위 대상=**가운데** 기본).
  - [Settings](../../app/Nexa.App/Settings.cs) `ViewOptions.UpNavTargetAlign`(기본 0.5) · `ShowHeaderInfo`(기본 true) 추가.
- **설정 TODO**: 대상 정렬(가운데/맨앞/맨뒤) + 헤더 정보란 on/off → [26 §8](../26-command-palette.md). 기본값은 구현됨, 선택 UI만 TODO.
- **검증**: app build 0 warn/err. ★실기 QA: (a) 폴더 진입 시 첫 항목 맨 위 / (b) 스크롤 내린 뒤 상위 이동 → 떠난 폴더가 **가운데** 선택 표시.

### E13-fix · 상위 이동 대상이 최상단일 때 빈 화면(QA #7)

- **증상**: 최상단(index 0, 예: addins) 폴더 진입 후 위로 → 화면 깜빡·**아무것도 안 보임**, 스크롤해야 보임(addins는 선택됨).
- **원인**: `ScrollIndexIntoView`가 `Reset` **직후 동기** 실행 → ItemsRepeater가 뷰포트 미실체화 상태에서 단일 요소만 강제 실체화+가운데 정렬 → 뷰포트 공백(+index 0 가운데 정렬은 위쪽 공백까지).
- **수정 1차(불완전)**: `ScrollIndexIntoView`를 `DispatcherQueue`(Low)로 지연. → **먼 인덱스(예: 30↓)에선 여전히 공백**(GetOrCreateElement+StartBringIntoView가 실체화 창↔스크롤 오프셋 불일치를 유발, 위 스크롤 무동작·아래로 스크롤해야 보임).
- **수정 2차(해결)**: `GetOrCreateElement`/`StartBringIntoView` 폐기 → **균일 행 높이 실측(`EstimateRowStride`)으로 목표 오프셋 계산해 `BodyScroll.ChangeView`로 정상 스크롤**. 가상화가 뷰포트를 정상 채워 공백 없음, 균일 높이라 가운데 정렬도 정확. (Reset 후 확정 위해 여전히 DispatcherQueue 지연.) **★실기 QA 통과(2026-07-02)** — 먼 인덱스·최상단 모두 공백 없이 정상. 상세 비교: [31 스크롤노트](../31-scroll-into-view-notes.md).

## 🔀 다른 PC 이어작업 핸드오프 (2026-07-02)

- **브랜치**: `refactor/001-audit` (원격 최신, PR #1 draft, CI green). 클론/pull 후 이어서.
- **완료(이 브랜치)**: 감사 → C1 코어 트리(nexa-tree)·ABI v3·로드검사 → MainWindow 가상화 배선 → F18 펼침유지 재구현 → 스크롤 위치 수정 → 크래시 로거. 코어 18 tests·app CI green.
- **실기 QA 통과**: 표시/필터/네비/펼침(F18 진입·위로)/선택/스크롤(진입 top·상위 center, 최상단·먼 인덱스 공백 없음).
- **남은 일(우선순위)**:
  1. **성능 슬라이스 4** — 탭 전환 클릭 지연(#4)·대용량 스크롤(#3): 탭별 트리 핸들 캐시 + 펼침/접힘 Reset→범위 diff + 행매핑 O(log n) + 10만 벤치(AC5).
  2. **E14 정리** — 미사용 C# 경로(`NativeInterop.ReadDir`/`SortItems`/`IsVisible`) 제거.
  3. **설정 화면** — 헤더 정보란 on/off(`ViewOptions.ShowHeaderInfo`)·상위이동 정렬(`UpNavTargetAlign`)·단축키 편집([26 §8](../26-command-palette.md)).
  4. **머지 전 점검**: F18/스크롤/선택 회귀 없는지 최종 QA 후 main 머지.
- **실행 주의**: IDE 내장 터미널에서 `dotnet run`은 즉시 종료(터미널 환경 이슈) → **pwsh 또는 빌드 exe** 사용. 미처리 예외는 `%LOCALAPPDATA%\NexaDir\crash.log`.

## E14 · 2026-07-02 · C1 슬라이스 4-1 — AC5 10만 노드 코어 벤치 → `(이 커밋)`

- **왜**: 슬라이스 4 착수 전 **어디가 병목인지 측정**(ADR-0004 §4는 "벤치 → 필요 시 O(log n)" 순서). 추측 대신 수치로 O(log n) 매핑 도입 여부 판단.
- **무엇 · 파일** ([nexa-tree/src/lib.rs](../../core/crates/nexa-tree/src/lib.rs)):
  - `#[cfg(test)] Tree::synthetic(dirs, per_dir)` — 파일시스템 없이 arena에 노드 직접 채움(열거 비용 배제, 순수 트리 연산만 측정).
  - `bench_100k_visible`(`#[ignore]`, 수동) — 100×1000=10만 파일 전체 펼침·`visible_index`·`row`·`select_all`·`collapse` 측정. 타이밍 단언은 CI 편차로 생략, `--nocapture` 출력.
  - `large_tree_scale_ops_complete`(상시) — 10만 노드에서 경계 행·위치조회·선택·접힘이 정상 완료(이차 폭주·패닉 가드).
- **측정 결과**(로컬, release): expand 100k행 **5.7ms** · row()×100k **0.8ms** · select_all **5.6ms** · collapse **1.9ms** — 전부 16ms 프레임 예산 안. `visible_index`는 O(n)이나 **단일 동작당 1회≈20µs**(10만) → **O(log n) 매핑 불채택**.
- **결론**: **코어는 10만 노드에서 이미 AC5 충족**. 실사용 병목은 C# 측(탭 재-Open·펼침 Reset 재실체화) → 4-2/4-3에서 처리.
- **검증(How)**: `cargo test -p nexa-tree` **10 green**(+scale gard), `cargo test -p nexa-tree -- --ignored --nocapture bench_100k`로 수치. fmt·clippy(-D warnings) 통과. **맥 완전 검증(코어 전용, WinUI 무관).**

## E15 · 2026-07-02 · C1 슬라이스 4-2 — 탭별 트리 핸들 캐시 → `(이 커밋)` ★실기 QA

- **왜**: QA #4(파일 많은 디렉터리 탭 전환 시 클릭 반응 지연) — 원인은 `SwitchToTab→Navigate→LoadDirectory→Open`이 **매 전환마다 새 트리 핸들 생성**(디스크 재열거 + 펼침 재적용). 이미 본 탭으로 돌아갈 때도 전량 재구성.
- **무엇 · 파일**:
  - [PanelTab.cs](../../app/Nexa.App/PanelTab.cs): 탭이 **자체 `VirtualTreeCollection Items`**(코어 핸들 소유) + `Loaded`(열림 캐시 여부) + `DirectChildCount`(헤더 복원용) 보유.
  - [MainWindow.xaml.cs](../../app/Nexa.App/MainWindow.xaml.cs):
    - `_leftItems`/`_rightItems`를 **활성 탭의 컬렉션 접근자**(`_leftTab.Items`)로 전환(기존 13개 사용부 무변경).
    - `LoadDirectory(bool,PanelTab)`로 시그니처 정리 + `PanelUi(left)`(그리드/헤더/경로바 3종) · `WireTab`(아이콘 콜백 배선) 헬퍼. 로드 성공 시 `tab.Loaded=true`.
    - **`ShowTab(left,tab)`**: 탭이 로드돼 있으면(`Loaded && Handle!=0`) **재-Open 없이 `ItemsSource`만 스왑** + 경로바·헤더·선택수 복원. 아니면 `LoadDirectory`. `SwitchToTab`이 `Navigate` 대신 이걸 호출.
    - `Navigate`/`ReloadBothPanels`는 캐시 무효화(경로 변경·필터 토글 시 `Loaded=false`) 후 재로드 — 비활성 탭도 다음 전환 때 재-Open.
    - `AddTab`는 `WireTab`, `CloseTab`은 닫는 탭 `Items.Dispose()`(핸들 회수).
- **효과**: 이미 연 탭으로의 전환이 **O(가시행 재실체화)만** — 디스크 재열거·펼침 재적용 제거. 선택·캐럿·펼침 상태도 탭에 그대로 보존.
- **검증(How)**: 맥 빌드 불가(WinUI) → **PR #1 CI `app` job green 필수**. 실기 QA: 파일 많은 폴더 여러 개를 탭으로 열고 전환 시 지연 감소 · 전환 후 선택/펼침/스크롤 유지 · 숨김/점 토글이 전 탭 반영 · 탭 닫기 후 크래시 없음.

## 진행 예정 (E16~)

- **E16 · 4-3 펼침/접힘 범위 diff 통지**: `ToggleExpand`가 `TreeRange`(start/removed/inserted)로 `Add`/`Remove` 통지(#3 스크롤 보존). 캐시 인덱스·캐럿 시프트 처리.
- **E17 · 3b-3 정리**: 미사용 C# 경로(`NativeInterop.ReadDir`/`SortItems`/`IsVisible`) 정리.
- **설정 화면**: QA #2(헤더 토글) 포함 — 표시 옵션·단축키·창 위치 편집 UI([26 §8](../26-command-palette.md)).

> ⚠️ **남은 MainWindow 재배선은 이 브랜치 최대 단일 변경** — WinUI 런타임 검증 불가(CI는 빌드만) → 실기 QA 필요. 회귀 위험 큰 만큼 위 E9/E10로 분할, 각 push마다 PR #1 CI(`app`) green 확인.

> ⚠️ **CI 참고**: 워크플로 트리거는 `push: [main]` + `pull_request`. 리팩토링 브랜치는 **PR을 열어야 CI가 돈다**. 앱(WinUI) 변경(E6·E7)은 맥 빌드 불가 → PR로 `app` job green 확인.
