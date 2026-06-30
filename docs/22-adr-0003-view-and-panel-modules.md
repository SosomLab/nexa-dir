# 22 · ADR-0003 — 확장 아키텍처: 뷰 모드 전략 & 도킹 패널 모듈

> 상태: **Accepted** (2026-06-30) · 관련: [21](21-adr-0002-fileview-control.md) 파일뷰 · [09](09-plugin-architecture.md) 플러그인 · [20](20-ui-layout.md) 레이아웃 · [01](01-architecture.md)

## 1. 맥락 / 요구
- **여러 뷰 모드**(아이콘·목록·계층(컬럼)·갤러리)를 **쉽게 교체·추가**하고 싶다.
- **하단 패널**에 상세정보·미리보기·Hex·터미널 등 **모듈**을 배치하고 **위치/크기/표시를 선택**하고 싶다.
- 공통 요구: **구현 로직을 쉽게 교체**하고 **확장·모듈화가 쉽도록 추상화 계층(Abstract/Interface)** 을 둔다.

## 2. 결정
1. **뷰 모드 = 전략(Strategy)**: 코어의 뷰 독립 데이터(가시 행 스트림 + 선택)를 `IFileView` 구현들이 각자 렌더. 런타임 교체.
2. **하단 패널 = 도킹 모듈**: `IToolPanel` 구현들을 `PanelHost`(도킹 매니저)가 배치/크기/표시/탭/플로팅으로 관리, 레이아웃 저장·복원.
3. 둘 다 **플러그인 확장점**([09](09-plugin-architecture.md)) — 새 뷰/패널은 **인터페이스 구현만**으로 추가, **코어 불변**.

## 3. 추상화 계층

### 3-1. 데이터 (코어, 뷰/패널 독립)
- `Provider`(저장소 추상, 기존 nexa-vfs) · `FileSource`(가시 행 VisibleRow 스트림) · `Selection`(`OrderedSet<NodeId>`, 교차폴더) · `SortSpec`/`FilterSpec`.
- **불변식**: 데이터·선택은 어떤 뷰/패널이 붙어도 동일. 뷰는 표현만 바꾼다.

### 3-2. 뷰 모드 (UI 전략) — `IFileView`
```
IFileView
  ViewKind  Kind { Icon, List(Details), Column, Gallery }
  void   Bind(FileSource source, Selection selection, SortSpec sort)
  void   ScrollTo(NodeId) / FocusItem(NodeId)
  event  ItemInvoked / ContextMenuRequested
  UIElement Root
```
| Kind | 구현 | 설명 |
| --- | --- | --- |
| **List(Details)** | `DetailsView` = **FileTreeGrid**([21](21-adr-0002-fileview-control.md)) | 이름/상태/크기/생성·수정일 컬럼 + 인라인 트리 + 교차선택 |
| **Icon** | `IconView` | 격자 아이콘, **크기 슬라이더**(이미지/동영상 직관). 가상화 그리드 레이아웃 |
| **Column** | `ColumnView` | **Miller 컬럼**(좌→우 폴더 트리 펼침, Mac식) |
| **Gallery** | `GalleryView` | 하단 가로 썸네일 스트립 + 상단 중앙 **큰 미리보기** |

→ 모두 같은 `FileSource`/`Selection` 공유. 뷰 전환은 `PaneHost`가 `IFileView` 교체.

### 3-3. 도킹 패널 (도구 모듈) — `IToolPanel`
```
IToolPanel
  string  Id / Title / Icon
  DockHint PreferredDock { Bottom, Right, Left, Float }
  UIElement CreateView()
  void    OnContext(Selection selection)   // 활성 선택/포커스 구독
PanelHost (도킹 매니저)
  register/show/hide · dock(위치) · resize(크기) · tab-group · float · 레이아웃 저장·복원
```
| 모듈 | 내용 | 단계 |
| --- | --- | --- |
| **DetailsPanel** | 단일 선택 = 메타데이터, **다중 선택 = 파일 목록 + 합계(개수/크기)** | M1 |
| **PreviewPanel** | 이미지/문서/코드 미리보기 | M2 |
| **HexPanel** | 바이너리 16진 뷰 | 후속 |
| **TerminalPanel** | **ConPTY 터미널(Cmd/Pwsh)**, 현재 경로 연동 | 후속 |

→ 패널은 `OnContext`로 활성 선택을 구독(상세/미리보기/Hex가 선택에 반응). 배치/크기/표시는 사용자가 선택, 세션 저장.

## 4. 근거
- **전략 + 플러그형**: 새 뷰/패널 = 인터페이스 구현만 → 교체·확장 쉽고 코어 격리.
- **뷰 독립 데이터**: 같은 선택/데이터를 여러 뷰가 공유 → 뷰 전환 시 상태 보존.
- **도킹 추상화**: 패널 위치/크기/표시를 데이터로 관리 → 레이아웃 저장·복원, 플러그인 패널 동일 취급.

## 5. 영향
- **코어(Rust)**: `FileSource`/`Selection`/`SortSpec` 안정 API([07](07-flagship-tree-multiselect.md) §3·4). 뷰/패널은 이를 구독.
- **앱(C#)**: `IFileView`(+4 구현) · `IToolPanel`(+모듈) · `PaneHost`(뷰 전환) · `PanelHost`(도킹). ADR-0002 `FileTreeGrid` = `DetailsView`.
- **플러그인([09](09-plugin-architecture.md))**: `IFileView`/`IToolPanel`을 1급 확장점으로 노출.
- **레이아웃([20](20-ui-layout.md))**: 도구 모음에 뷰 전환, 하단에 도킹 패널 영역.

## 6. 구현 단계
- 뷰: `DetailsView`(진행, [21](21-adr-0002-fileview-control.md)) → `IconView` → `ColumnView` → `GalleryView`.
- 패널: `DetailsPanel`(M1) → `PreviewPanel`(M2) → `TerminalPanel`(ConPTY) → `HexPanel`.
- 인프라: `IFileView`/`IToolPanel` 인터페이스 + `PaneHost`/`PanelHost` 골격 우선(추상화 먼저, 구현은 점진).
