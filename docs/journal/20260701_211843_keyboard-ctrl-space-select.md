# 작업 기록 — 2026-07-01 21:18 (KST)

> 기록 ID: `20260701_211843_keyboard-ctrl-space-select`
> 이전 기록: `20260701_211007_keyboard-range-expand`

## 1. 요청(연속 명확화)
- **Ctrl+↑/↓** = 위치(캐럿)만 이동. **Space** = 선택.
- 사용자 보충: "Ctrl = **비연속 다중 선택** 모드", "**이 경우에만**(Ctrl) Space로 다중 선택 가능".
  → Ctrl+Space = 토글(비연속 다중), Space 단독 = 단일 선택으로 해석(Explorer 방식).

## 2. 구현
- **캐럿 시각화**: `DirItem.IsCaret` 추가 → 선택 안 돼도 **얇은 포커스 외곽선**(`CaretBorderBrush`)을 `RowBorderBrush`에 반영(선택 > 캐럿 > 없음). Ctrl 이동 시 위치 식별.
- **`MoveCaret(left, item)`** 헬퍼: 이전 캐럿 `IsCaret` 해제 + 새 캐럿 설정(패널별 1개). 클릭·키보드 공용.
- **`OnGridKeyDown` 확장**:
  - **Ctrl+↑/↓**: 선택 불변, `MoveCaret`로 위치만 이동(early return).
  - **Space**: `cur` 항목 — Ctrl면 `IsSelected` 토글(나머지 유지), 아니면 단일 선택. anchor=해당 항목.
  - 기존 Shift 범위·단일 이동·→/← 펼침접힘 유지.
- 선택 개수 표시를 `UpdateSelectionCount`로 공용화. `OnRowPointerPressed`도 `MoveCaret` 사용(클릭 시 캐럿 외곽선).

## 3. 검증
- `dotnet build app/Nexa.App -c Debug` → **0/0**. (IDE의 StatusText/DirGrid 미존재 진단은 XAML 코드젠 전 일시적)
- 수동: Ctrl+↓/↑ 외곽선만 이동 → Ctrl+Space 반복 시 비연속 다중 선택, Space 단독 단일 선택.

## 4. 다음(백로그)
- **Ctrl+A** 전체 선택, Home/End·PageUp/Down, ← 부모 점프. 이후 F15 후속(설정 JSON)·A5 정렬.
