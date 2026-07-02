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
├─ E4 진행 로그(이 문서) ......................... (이 커밋)
└─ E5~ C1 슬라이스 2(ABI)·3(앱 재배선)·4(성능) ... 예정
```

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

## 진행 예정 (E5~)

- **E5 · C1 슬라이스 2**: `nexa-interop` C ABI — `nexa_tree_open/close/visible_len/row/expand/collapse/select…` + `NexaRow`/`NexaRange` + **ABI v3(로드 시 실제 검사)** + 라운드트립 테스트.
- **E6 · C1 슬라이스 3**: 앱 재배선 — `MainWindow`의 C# `ExpandInPlace`/`IsSelected`를 코어 `VisibleRow` 소비로 교체(`ItemsRepeater` 가상화 + diff 반영).
- **E7 · C1 슬라이스 4**: 10만 노드 성능 벤치(AC5) + 행 인덱스↔노드 매핑 O(log n).
