# 작업 기록 — 2026-07-01 23:47:55 (KST) · 진행 현황 업데이트 + 구현 분석

> 기록 ID: `20260701_234755_progress-review-analysis`
> 맥에서 저장소 최신화(Windows 진척 pull) 후 현황 갱신 + 구현 코드 분석.

## 1. 현황 업데이트
- STATUS §6 표를 **F1~F17**로 확장(레이아웃 정교화 F7~9 · 플래그십 초안 F10/F11/F17 · 네비/메뉴/UX F12~16).
- 코어 `cargo test --workspace` **9 tests green(맥 실측)**.

## 2. 구현 분석 (요지)

### 잘된 점
- **인터롭 경계 견고**: 핸들 기반 스트리밍(`nexa_dir_open/next/close`), `NexaEntry`. `CString` 수명 핸들 소유,
  널 체크, 엔트리 오류 skip, `wrapping_add`로 FFI 경계 패닉 회피 → 메모리 안전·오류 격리 원칙 부합.
- **컴포넌트화**: `NexaFileGrid`(도메인 비종속 재사용 컨트롤, 독립 제품 ADR-0002), `NexaMenuBar`(초박형 커스텀),
  `ColumnResizeGrip`(ProtectedCursor 우회) — `Nexa.Controls` 라이브러리로 분리(490줄).
- **UX 완성도**: 클릭 즉시반응(PointerPressed), 포커스아웃 회색, 캐럿↔anchor 분리, 자석 스냅(재진입 가드),
  키보드 Shift 범위·Ctrl+Space 비연속·→/← 펼침 = Explorer급.
- **설정 준비**: 폴더우선 정렬 옵션화(F15, `SortOptions`) — 하드코딩 제거.

### 아키텍처 부채 / 리스크 (개선 방향)
1. **플래그십 모델이 앱(C#) 계층에 초안으로 존재** — 인라인 펼침(F10)은 `ReadDir` 재귀 삽입을
   `ObservableCollection`에. 계획(DR·[07])의 **코어 `VisibleRow` 평면 스트림(C1)** 미구현 →
   **10만 노드 가상화·NFR-P1/P2 미달 우려**. 다음 우선: 트리/선택 모델을 **Rust 코어로 이관**.
2. **MainWindow.xaml.cs 비대(721줄) — God-object**: 네비/선택/키보드/스냅/토글 + **패널별 상태 중복**
   (`_leftItems/_rightItems`·`_leftNav/_rightNav`·`_left/_right` 38회 분기). "얇은 UI" 원칙·MVVM에서 이탈.
   → **`Panel`(또는 PanelViewModel) 추상화 + `Nexa.ViewModels`(net8.0) 분리**(맥 빌드/테스트) 필요.
   특히 **탭 도입 전** 리팩터 권장(탭마다 상태 → `bool left` 분기로는 확장 불가).
3. **앱 자동 테스트 0** — 검증이 전부 수동(Windows). 로직 증가에 회귀 위험 → 로직을 net8.0 VM으로 빼고 단위테스트.
4. **미구현(초안 한계)**: 러버밴드 드래그 선택 · 교차폴더 다중선택 완성(C3/C4) · **파일 작업(복사/이동/삭제 D)**
   · 탭 모델(placeholder) · 설정 JSON 영속화 · 경로 바.

### 상태 요약
- **동작하는 것**: 좌/우 듀얼 목록, 폴더 진입·뒤로/앞으로/위로, 인라인 펼침+4컬럼, 단일/다중/범위/키보드 선택,
  커스텀 메뉴/스플리터/토글. (한 패널의 펼친 트리 내 **교차폴더 선택 UX는 이미 프로토타입 동작**)
- **못하는 것**: 대규모 성능(코어 스트림 부재), 파일 작업, 탭, 설정 저장, 러버밴드.

## 3. 권장 다음 단위(순서)
1. `Nexa.ViewModels`(net8.0) 분리 + `Panel` 추상화(패널별 상태 캡슐화) — 맥 테스트 가능, 탭 준비.
2. 코어 `VisibleRow` 평면 스트림(C1) — 인라인 트리/선택을 코어 모델 위로 재기반(가상화·성능).
3. 파일 작업(D) 초안 → 혼합 선택 대상.
