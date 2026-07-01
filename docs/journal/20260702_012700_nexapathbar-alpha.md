# 작업 기록 · NexaPathBar α — 계층 경로 바 (F23)

> 요구/설계: docs/27. α = 브레드크럼 클릭 이동 + 우클릭 텍스트 편집.

## 구현
- Nexa.Controls/NexaPathBar(UserControl): Path DP, Navigated 이벤트(네비 비종속).
  브레드크럼=ItemsRepeater(세그먼트: 클릭 이동·현재 무동작·hover·드라이브 C:→C:\),
  편집=TextBox(우클릭 진입·전체선택·복붙·Enter 이동·Esc/포커스아웃 복귀). PathSegment 모델.
- MainWindow: 좌/우 네비 바 PathText→NexaPathBar(PathBarL/R, 버튼|경로 2열 Grid).
  LoadDirectory가 pathBar.Path 설정, OnPathBarNavigated(Directory.Exists→Navigate, 없으면 상태바·복귀).
- docs/19 F23, 05 FR-A2b(컴포넌트 참조), README(27).

## 검증
- WinUI라 맥 빌드 불가 → push 후 CI(windows) app job green 확인.
- Windows 수동: 세그먼트 클릭 이동, C:→C:\, 우클릭 편집 붙여넣기+Enter 이동, Esc/바깥클릭 복원.

## 후속(β/γ)
- 오버플로 …/UNC · 형제 ▾ 드롭다운 · 드롭 타깃 · VFS 스킴 · 템플릿드 컨트롤화.
