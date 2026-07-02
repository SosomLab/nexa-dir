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
└─ E8~ C1 슬라이스 3b-2(열거 재배선)·3b-3(선택)·4(성능) . 예정
```

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

## 진행 예정 (E8~)

- **E8 · C1 슬라이스 3b-2**: `nexa_tree_*` 함수 P/Invoke + 패널당 `TreeHandle` 보유, `LoadDirectory`를 코어 트리 열기+가시행(`visible_len`/`row`) 소비로 전환(현 `ReadDir`/`DirItem` 채움 대체). 펼침/접힘은 다음.
- **E9 · C1 슬라이스 3b-3**: 펼침/접힘(`nexa_tree_expand/collapse` + `RangeChange` diff) + 선택(`nexa_tree_select*`) 위임, C# `ExpandInPlace`/`CollapseInPlace`/`ApplySavedExpansion`/`SortItems` 제거.
- **E10 · C1 슬라이스 4**: 10만 노드 성능 벤치(AC5) + 행 인덱스↔노드 매핑 O(log n).

> ✅ **CI(PR #1)**: 각 앱 변경 push마다 재실행 — `app` job green 확인하며 진행.

> ⚠️ **CI 참고**: 워크플로 트리거는 `push: [main]` + `pull_request`. 리팩토링 브랜치는 **PR을 열어야 CI가 돈다**. 앱(WinUI) 변경(E6·E7)은 맥 빌드 불가 → PR로 `app` job green 확인.
