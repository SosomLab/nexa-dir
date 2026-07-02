# 29 · ADR-0004 — 코어 트리/선택 모델 (VisibleRow 스트림, C1)

- 상태: **Accepted** (2026-07-02) · 관련: [07 플래그십](07-flagship-tree-multiselect.md)·[01 아키텍처](01-architecture.md)·[10 결정](10-decision-record.md)·[refactor-001 감사](journal/20260702_130615_refactor-001-audit.md)
- 대상 단위: **C1**(백로그 STATUS §6). 리팩토링 감사 A1/A2의 “핵심 정정”.

## 맥락 (문제)

인라인 트리 펼침·교차 선택이 현재 **앱(C#) 코드비하인드**에 축적돼 있다(`MainWindow.xaml.cs` `ExpandInPlace`/`CollapseInPlace`/`DirItem.IsSelected` bool, `NativeInterop.ReadDir`가 C#에서 정렬·가시성 필터). 이는:
- 설계(`docs/01 §3` “핫패스=코어”, `docs/07 §3-4` “가시 노드 평면 스트림 + `OrderedSet<NodeId>` 선택”)와 배치,
- **O(n) UI-스레드 연산**으로 AC5(10만 노드 <150ms/60fps, NFR-P1/P2)를 못 맞추며,
- 맥에서 단위테스트 불가.

## 결정

트리/선택 모델을 **Rust 코어 신규 크레이트 `nexa-tree`**로 이관한다. 트리를 **가시 노드의 평면 스트림(`VisibleRow`)** 으로 투영하고, 펼침/접힘/선택을 코어 명령으로 처리해 **행 범위 diff**만 UI에 흘려보낸다.

### 크레이트 배치
- `core/crates/nexa-tree` — `nexa-core`(FileKind)·`nexa-vfs`(스트리밍 열거)에 의존. **UI 비종속 순수 로직** → 맥 단위테스트.
- 소비: `nexa-interop`(C ABI, 슬라이스 2) → `Nexa.App`(슬라이스 3).

### 핵심 타입 (슬라이스 1)
```
NodeId = u64                       // 트리 세션 내 안정(삽입 시 순번, 미회수)
Node { id, parent:Option<NodeId>, path, name, kind, depth,
       size, modified_unix_ms, attrs, expanded, loaded, children:Vec<NodeId> }
VisibleRow { id, depth, kind, name, size, modified_unix_ms, attrs,
             expanded, has_children }   // UI 단위(코어→호스트)
RangeChange { start, removed, inserted }  // 펼침/접힘 diff
Tree { nodes(arena), roots, visible, sel_order+sel_set(OrderedSet), anchor, root_path }
```

### 동작 규약
- **지연 로딩**: `expand(id)`는 `loaded=false`면 `nexa-vfs`로 자식 스트리밍 열거→arena 추가·정렬(폴더 우선+이름), `expanded=true`. `visible`에 **부분 트리 DFS**(자식들의 이전 펼침 상태 복원)를 삽입 → `RangeChange{start,0,inserted}`.
- **접힘**: `collapse(id)`는 `expanded=false`, `visible`에서 `depth>node.depth`인 후속 연속 구간 제거 → `RangeChange{start,removed,0}`. **자식의 펼침 상태는 보존**(재펼침 시 복원).
- **선택 = `OrderedSet<NodeId>`**(삽입 순서 보존): `select_single/toggle/select_range(anchor~current, 가시 순서)/select_all_visible/clear`. 임의 부모 혼합 허용(교차 선택 불변식).
- **정렬**: 부모별 비교자(현재 = 폴더 우선 + 이름 오름차순, 앱 `SortItems`와 동일). 펼침/선택 보존한 채 재정렬은 후속.

### ABI 스케치 (슬라이스 2, ABI v3)
```
nexa_tree_open(path) -> *mut Tree | null
nexa_tree_close(*mut Tree)
nexa_tree_visible_len(*mut Tree) -> u64
nexa_tree_row(*mut Tree, index, *mut NexaRow) -> c_int      // 1/0
nexa_tree_expand(*mut Tree, id, *mut NexaRange) -> c_int    // 지연 열거 → diff
nexa_tree_collapse(*mut Tree, id, *mut NexaRange) -> c_int
nexa_tree_select(*mut Tree, id, mode) / range / all / clear
nexa_tree_selected_len / nexa_tree_selected_path(index) -> *const c_char
```
- `NexaRow.name`은 “다음 호출 전까지 유효” 규약(기존 `NexaEntry`와 동일, 호스트가 즉시 복사).
- ABI 버전 v2→v3. **이번엔 로드 시 실제 검사**(감사 A3): 호스트가 `nexa_abi_version()==3` 확인 후에만 네이티브 경로 사용.

## 슬라이스 계획 (수직, 초안→확장, 각 ≈1커밋)
1. **`nexa-tree` 모델**(이 저장소 슬라이스 1) — 위 타입 + open/expand/collapse/visible/selection + **단위테스트**(임시 폴더, 맥 green). ABI·앱 미접촉.
2. **C ABI**(nexa-interop) — 위 함수 + `NexaRow`/`NexaRange` + ABI v3 + 라운드트립 테스트.
3. **앱 재배선** — `MainWindow`의 C# 트리/선택을 코어 `VisibleRow` 소비로 교체(`ItemsRepeater` 가상화 + diff 반영). C# `ExpandInPlace`/`IsSelected` 경로 제거.
4. **성능 검증** — 10만 노드 벤치(AC5), 필요 시 행 인덱스↔노드 매핑 O(log n)(부모별 자식수 캐시) 도입.

## 범위 밖(후속)
- 외부 변경(watcher) 반영·경로변동 시 선택 자동정리, 순환 심링크 사이클 차단, 정렬 변경 유지, 폴더 선택 시 하위 흡수(중복 제거), 부모별 다른 정렬키. (docs/07 §4·§9)
- 심링크(디렉터리) 펼침 — 슬라이스 1은 `kind==Dir`만 펼침 가능(보수적).

## 대안 (기각)
- **C# 유지+최적화**: NFR 미달·맥 테스트 불가·설계 배치(감사 A1/A2). 기각.
- **nexa-vfs에 트리 병합**: vfs는 열거/Provider 책임에 집중, 트리 상태는 별 크레이트가 응집도↑. 기각.
