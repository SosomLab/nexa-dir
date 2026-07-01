# 27 · NexaPathBar — 계층 경로 바 커스텀 컴포넌트 (설계)

> 요구: 탭 하단에 **전체 경로**를 세그먼트로 표시하고, ① 세그먼트 클릭 이동(현재 세그먼트는 무동작·hover 없음, 드라이브 `C:`→`C:\`),
> ② **우클릭 → 텍스트 편집 모드**(전체 경로 선택·복사/붙여넣기·Enter 이동·포커스아웃 시 무시·복귀). 요구 **FR-A2/FR-A2b**.
> 한 위치에 여러 동작이 모이므로 **전용 커스텀 컴포넌트**로 개발. 위치: `Nexa.Controls`(재사용·제품화, [21](21-adr-0002-fileview-control.md) §9).

---

## 0. 의견 (결론)

- **커스텀 컴포넌트가 맞다.** 브레드크럼/편집 두 모드 + 세그먼트별 hover·클릭·(후속)드롭다운·드롭 타깃이 한 컨트롤에 응집 → 재사용/테스트/스타일링을 위해 분리.
- **이름: `NexaPathBar`** 권장(제안하신 `NextPathText`보다 역할이 분명 — "Text"가 아니라 브레드크럼 **바**). 기존 명명(`NexaFileGrid`/`NexaMenuBar`)과 일관.
- **Nexa.Controls**에 배치 → 독립 판매 가능 컨트롤 라인업에 합류.

## 1. 기능

1. 전체 경로를 **드라이브 + 폴더 세그먼트**로 표시(1줄).
2. **세그먼트 클릭 → 그 경로로 이동**(`Navigated` 이벤트로 통지).
3. **현재(마지막) 세그먼트**: 비활성 — hover/클릭 없음(이미 그 위치).
4. **드라이브 세그먼트** `C:` 클릭 → `C:\`.
5. **hover**: 클릭 가능한 세그먼트만 버튼처럼 강조(PointerOver VisualState).
6. **우클릭 → 편집 모드**: 전체 경로를 텍스트로 표시·**전체 선택**·복사/붙여넣기.
7. 편집 모드 **Enter → 그 경로로 이동**. **포커스아웃/Esc → 입력 무시·브레드크럼 복귀**.
8. **오버플로(긴 경로)**: 앞부분 `…` 축약 또는 수평 스크롤, **현재(꼬리) 항상 표시**.
9. (후속) 세그먼트 **`▾` → 형제 폴더 드롭다운**(FR-A2), 세그먼트 **드롭 타깃**(드래그앤드롭 복사/이동).

## 2. 두 모드 + 상태 전이

```
[Breadcrumb] --우클릭/빈영역 클릭--> [Edit]
   ▲                                   │
   └── Enter(이동) · Esc · 포커스아웃 ──┘
```
- **Breadcrumb(기본)**: 세그먼트 스트립. 클릭=이동, 현재 세그먼트 비활성.
- **Edit**: `TextBox`(전체 경로, 전체 선택). Enter→제출(이동), Esc/LostFocus→취소(원복). 복사/붙여넣기 네이티브 지원.

## 3. 공개 API (확장성 — DependencyProperty/이벤트)

| 멤버 | 종류 | 설명 |
| --- | --- | --- |
| `Path` | DP `string` | 표시할 전체 경로(호스트가 활성 탭 경로 바인딩). |
| `Separator` | DP `char`=`\` | 구분자. **VFS는 `/`** 등으로 교체(확장). |
| `IsEditing` | DP `bool`(읽기) | 현재 편집 모드 여부. |
| `Navigated` | event | 세그먼트 클릭·편집 제출 시 `{ string Path }` 통지. **이동은 호스트가 수행**(컴포넌트는 통지만). |
| `Segmenter` | 속성 `IPathSegmenter` | 경로→세그먼트 분해 훅(§4). 기본=로컬 FS. |
| `SiblingsProvider` | 속성 `Func<string, ...>` | (후속) `▾` 형제 폴더 열거(비동기). |
| 테마 브러시들 | DP | hover/현재/편집 배경·전경(재스타일). |

> **네비게이션 비종속**: 컨트롤은 파일시스템/탭/Nav를 모른다 → `Navigated(path)`만 raise, 호스트(MainWindow)가 `Navigate(left, path, record:true)`로 처리. 테스트·재사용 용이.

## 4. 확장성 설계

- **세그먼테이션 추상화** `IPathSegmenter`: `IReadOnlyList<PathSegment> Split(string path)`, `PathSegment { string Label; string FullPath; bool IsRoot; }`.
  - 기본 구현: **로컬 FS**(드라이브 `C:`→`C:\`, 폴더). 추가: **UNC**(`\\server\share\...`), **VFS 스킴**(`ftp://host/a/b`, `s3://bucket/key`). → 컴포넌트가 OS/FS를 하드코딩하지 않음.
- **템플릿드 컨트롤**(`ControlTemplate` + parts: `PART_Breadcrumb`(ItemsRepeater), `PART_Editor`(TextBox)) → 완전 재스타일.
- **DP 중심** → 바인딩/애니메이션/스타일 친화. 플러그인/설정에서 동작·외형 확장.
- 후속 훅: 형제 드롭다운, 드롭 타깃, 세그먼트 컨텍스트 메뉴(경로 복사 등).

## 5. 성능 확보 방법

- **세그먼트 = `ItemsRepeater`(요소 재활용)**: 깊은 경로도 컨테이너 재사용. **`Path` 변경 시에만** 세그먼트 모델 재계산(hover는 VisualState — 코드/재빌드 없음).
- **편집은 네이티브 `TextBox`**(효율적). 모드 전환은 두 파트 Visibility 토글(재생성 X).
- **오버플로 계산은 `SizeChanged` 시 1회**(상시 측정 금지): 폭 부족 시 앞 세그먼트를 `…`로 접고 현재/꼬리 우선 표시.
- **형제 열거(▾)는 열 때만**, 코어(비 UI 스레드) 비동기(`IAsyncEnumerable`), 결과 캐시.
- **단일 라인 고정 높이** → measure/arrange 경량. 세그먼트 라벨은 값 변경 시에만 갱신(INPC).
- 대량 네비게이션 시에도 경로 세그먼트 수는 작음(≈수~수십) → 실질 비용 낮음. 초점은 **불필요한 재구성 제거**.

## 6. 호스트 연동 (MainWindow)

- 현재 네비 바의 `PathText`(TextBlock) → **`NexaPathBar`로 교체**. `Path` = 활성 탭 경로(`Navigate` 시 갱신).
- `Navigated` 구독 → `Navigate(left, e.Path, record:true)`. 좌/우 패널 각각 인스턴스.
- 편집 제출 경로 유효성(존재 X) → 호스트가 검증·실패 시 상태바 안내(컨트롤은 통지만).

## 7. 단계적 구현

- **α (핵심)**: 브레드크럼(세그먼트 클릭 이동·현재 비활성·hover, 드라이브 `C:`→`C:\`) + 편집 모드(우클릭·전체선택·복붙·Enter 이동·Esc/포커스아웃 복귀). 로컬 FS Segmenter.
- **β**: 오버플로 `…`/스크롤, UNC 세그먼트, 경로 복사 컨텍스트.
- **γ**: 형제 `▾` 드롭다운, 세그먼트 드롭 타깃(드래그앤드롭), VFS 스킴 Segmenter(원격 경로).

## 8. 로드맵/요구 연결
- **FR-A2/FR-A2b**([05](05-requirements.md)) 실현 컴포넌트. [20](20-ui-layout.md) 네비 바 영역. 재사용 컨트롤 원칙 [21](21-adr-0002-fileview-control.md).
- 구현 시 [19](19-implemented-features.md)에 F 항목 추가, `Nexa.Controls/NexaPathBar.*` 신설.
