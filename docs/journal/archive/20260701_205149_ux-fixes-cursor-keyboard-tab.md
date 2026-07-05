# 작업 기록 — 2026-07-01 20:51 (KST)

> 기록 ID: `20260701_205149_ux-fixes-cursor-keyboard-tab`
> 이전 기록: `20260701_203700_command-palette-design`

## 1. 요청(사용자 피드백 3건)
1. 그리드 컬럼 사이 마우스 오버 시 **너비 조정 커서로 안 바뀜** → 크기 변경 커서로.
2. **키보드 이동 안 됨**.
3. **탭 이름(탭 1/홈/탭 2) 클릭 시 해당 패널 활성화** 보완.

## 2. 원인 · 조치
1. **커서**: 리사이즈 핸들이 일반 `Border` — WinUI 3는 커서를 `UIElement.ProtectedCursor`(protected)로만 지정 가능.
   → **`ColumnResizeGrip`(Grid 기반) 서브클래스 신설**, 생성자에서 `ProtectedCursor = InputSystemCursor.Create(SizeWestEast)`. [NexaFileGrid.xaml](../../app/Nexa.Controls/NexaFileGrid.xaml)의 핸들을 이 타입으로 교체.
2. **키보드**: `KeyDown`이 XAML 배선이라 **내부 `ScrollViewer`가 방향키를 먼저 Handled 처리하면 그리드 핸들러가 안 걸림**.
   → 생성자에서 `DirGrid/DirGrid2.AddHandler(KeyDownEvent, OnGridKeyDown, handledEventsToo:true)`로 재배선, XAML `KeyDown` 제거. (핸들러 로직·`e.Handled`는 기존 유지)
3. **탭 클릭**: 좌/우 탭 바 `Border`에 `Tag="L"/"R"`·`Tapped="OnTabBarTapped"` 추가 → `SetActivePanel(left)` + 그 그리드 `Focus`(키보드 이동 대상).

## 3. 검증
- `dotnet build app/Nexa.App -c Debug` → **0/0**. (IDE의 `DirGrid does not exist` 진단은 XAML 코드젠 전 일시적 — 빌드에서 해소)
- 수동 확인 항목: 헤더 경계 커서 변경 · 행 클릭 후 ↑/↓ 이동 · 비활성 패널 탭 클릭 시 활성 전환.

## 4. 다음
- 사용자 테스트 후 이상 시 iterate. 이후: F15 후속(설정 JSON 영속화 + 보기 메뉴 토글) 또는 A5 정렬.
