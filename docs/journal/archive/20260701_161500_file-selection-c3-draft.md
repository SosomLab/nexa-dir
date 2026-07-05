# 작업 기록 — 2026-07-01 16:15 (KST)

> 기록 ID: `20260701_161500_file-selection-c3-draft`
> 이전 기록: `20260701_155500_inline-tree-finder-columns`

## 1. 요청
- 파일 클릭 선택(다중·범위·드래그 선택 등) 기능 개발.

## 2. 구현 (F11, 플래그십 C3 초안)
- **DirItem**: `IsSelected` + `RowBackground`(선택=반투명 파랑, 비선택=투명 — 전폭 클릭 히트 유지).
- **OnRowTapped**(수정자별):
  - 단일 = 나머지 해제 후 선택 / Ctrl = 토글 다중 / Shift = 기준점~클릭 범위(Ctrl+Shift=추가).
  - 패널별 기준점(`_leftAnchor`/`_rightAnchor`), 새 폴더 로드 시 기준점 초기화.
  - 수정자 키 = `InputKeyboardSource.GetKeyStateForCurrentThread`(`KeyDown` 헬퍼).
  - 상태바 "N개 선택됨".
- **행 템플릿**: 루트 StackPanel 전폭 `Background`(하이라이트)+`Tag`+`Tapped`. 디스클로저 클릭은 `e.Handled`로 선택과 분리.
- 메뉴 바 Fit(높이 24 + MenuBarItem 스타일)도 반영(사용자 조정).

## 3. 검증
- `dotnet build app/Nexa.App -c Debug` → **0/0**. 단일/Ctrl/Shift 선택·하이라이트·상태바 확인.

## 4. 다음 (백로그)
- **드래그(러버밴드) 선택**: `NexaFileGrid` 내부에 선택 오버레이+교차 히트테스트(컨트롤 단위). Ctrl+A·방향키 선택.
- 폴더 교차 선택 유지 · 혼합 파일 작업(C4) · 헤더 드래그 재정렬(컬럼-구동化).
