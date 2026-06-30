# 21 · ADR-0002 — 파일 뷰 컨트롤 (트리-그리드)

> 상태: **Accepted** (2026-06-30) · 관련: [07](07-flagship-tree-multiselect.md) 플래그십 · [01](01-architecture.md) §4 · [20](20-ui-layout.md)

## 1. 맥락
플래그십([07](07-flagship-tree-multiselect.md))과 클라우드/원격([05](05-requirements.md) FR-G)이 파일 뷰에 **4가지를 동시에** 요구한다:
1. **트리**(인라인 폴더 확장·접힘, 진입과 별개)
2. **다중 컬럼**(이름 / **상태(로컬·온라인·동기중)** / 크기 / 수정일 / 종류)
3. **폴더 교차 다중 선택**(서로 다른 부모의 항목 임의 선택)
4. **10만 가시 노드 가상화**(NFR-P1/P2: 첫 렌더 <150ms, 60fps)

추가로 행 단위 **셸 컨텍스트 메뉴**(OneDrive "온라인에서 보기" 등 호스팅) + 고유 항목.

## 2. 결정
WinUI 기성 단일 컴포넌트(ListView/TreeView/DataGrid)를 **쓰지 않고**,
**`ItemsRepeater` 기반 커스텀 트리-그리드 컨트롤**(가칭 `FileTreeGrid`)을 구현한다.
- 트리는 코어의 **가시 행(VisibleRow) 평면 스트림**으로 투영([07](07-flagship-tree-multiselect.md) §3).
- 컬럼은 **행 템플릿 Grid + 공유 컬럼 정의 + 헤더 행**.
- 선택은 코어 **`OrderedSet<NodeId>`**(부모 무관)로 직접 관리([07](07-flagship-tree-multiselect.md) §4).

## 3. 대안 비교
| 컴포넌트 | 가상화 | 다중 컬럼 | 트리(계층) | 교차폴더 선택 | 판정 |
| --- | :---: | :---: | :---: | :---: | --- |
| ListView | ✅ | 직접 | ❌ | 평면만(트리 의미✕) | 트리 불가 |
| TreeView | ⚠️약함 | ❌ | ✅ | ❌(계층 종속) | 컬럼·교차선택·대규모 ✕ |
| DataGrid(CommunityToolkit) | ✅ | ✅ | ❌ | 제한 | 트리 불가 |
| **ItemsRepeater** | ✅(저수준) | 직접 | 데이터로 | 직접 | **4요건 충족 ← 채택** |

기성은 4요건을 **동시** 충족하지 못한다. WinUI엔 통합 TreeDataGrid 부재.

## 4. 근거 — 4요건 → ItemsRepeater 매핑
- **트리 → 평면화**: 코어가 펼침 상태 반영 평면 리스트(VisibleRow: id, depth, expanded, hasChildren, meta[columns]) 산출 → 컨트롤은 평면 리스트만 가상화(트리 모름). **가상화 문제가 평면 리스트 가상화로 환원**. 들여쓰기=depth, ▶/▼=행 템플릿. 행↔노드 매핑은 부모별 자식수 캐시로 O(log n).
- **컬럼**: 행 = `Grid`(공유 컬럼 너비, 헤더와 동기) + 별도 헤더 행. **상태 컬럼 = VFS 메타의 sync 상태**(provider가 채움).
- **교차선택**: `OrderedSet<NodeId>` — 기성 선택 모델 미사용(그 한계가 채택 이유). Ctrl(토글)/Shift(가시 범위)/Ctrl+A.
- **셸 메뉴**: 행 우클릭 → `MenuFlyout` + 셸 `IContextMenu` 호스팅(컨트롤 무관).

## 5. 부분 재사용 (바닥부터는 아님)
- **가상화 엔진**: ItemsRepeater (직접 구현 안 함).
- **컬럼 리사이즈/패널 분할**: CommunityToolkit Sizers.
- **컨텍스트 메뉴**: COM `IContextMenu` 호스팅 계층(C#).
- DataGrid/TreeView **자체는 미채택**.

## 6. 비용 / 리스크 / 완화
- 부담: 선택·키보드 내비·컬럼 헤더/리사이즈·접근성을 직접 구현.
- 완화: **플래그십이 제품 제1 차별화** → 투자 정당. 아래 단계로 리스크 분산. 접근성은 ItemsRepeater AutomationPeer 보강.

## 7. 구현 단계 (점진)
1. ✅ ItemsRepeater 평면 목록(현재, [19](19-implemented-features.md) F5)
2. 컬럼 헤더 + **컬럼 시스템**(기본/사용자정의 수식/플러그인열, 듀얼 동기화 → [23](23-column-system.md))
3. 트리 행(depth 들여쓰기 + ▶/▼, 코어 `expand` 연동)
4. 선택 모델(교차폴더 `OrderedSet`, Ctrl/Shift/Ctrl+A)
5. 가상화·성능 튜닝(10만, NFR-P1/P2 벤치)
6. 셸 컨텍스트 메뉴 + 클라우드 상태 컬럼 공급

## 8. 영향
- **코어(Rust)**: VisibleRow 스트림 · Selection(OrderedSet) · `expand`/`collapse` API([07](07-flagship-tree-multiselect.md) §3·4). VFS `Entry` 메타에 **sync 상태**(로컬/온라인/동기중) 필드 추가(FR-G).
- **앱(C#)**: 커스텀 `FileTreeGrid`(ItemsRepeater 기반) + 헤더/컬럼/선택/컨텍스트 메뉴. 현재 `MainWindow`의 ItemsRepeater가 출발점.

## 9. 재사용 라이브러리 분리 (`Nexa.Controls`)

ItemsRepeater 기반 컨트롤은 **별도 WinUI 클래스 라이브러리 `app/Nexa.Controls`** 로 분리해 다른 기능에서도 가져다 쓴다.

- **도메인 비종속 컨트롤**: `VirtualizedTreeGrid` — 행/컬럼/선택을 **추상 인터페이스**(`IRowSource` · `IColumn` · `ISelectionModel`)로 받는다. **파일 개념을 모른다**.
- **파일 특화 = 어댑터**: `FileTreeGrid` = `VirtualizedTreeGrid` + 파일 `RowSource`(코어 `FileSource` 바인딩). 어댑터는 앱/도메인 측.
- **재사용처**: 파일 목록 · 검색 결과 뷰 · 클라우드 브라우저 · 플러그인 패널 등 **가상화 트리-그리드가 필요한 모든 곳**.
- **의존 경계**: 컨트롤은 **ItemsRepeater + CommunityToolkit Sizers만** 참조(코어/도메인 **비참조**) → 향후 **NuGet 패키지화 가능**. 계층: 컨트롤(표현/상호작용) ↔ 어댑터(도메인 매핑) ↔ 코어(데이터).
- **테스트**: 컨트롤은 도메인 없이 단위 테스트 가능(가짜 `IRowSource`로 가상화·선택·컬럼 검증).
- ADR-0003의 `IFileView` 구현(`DetailsView`)은 이 `VirtualizedTreeGrid`를 사용한다.

