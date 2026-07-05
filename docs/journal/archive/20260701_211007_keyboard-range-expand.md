# 작업 기록 — 2026-07-01 21:10 (KST)

> 기록 ID: `20260701_211007_keyboard-range-expand`
> 이전 기록: `20260701_205149_ux-fixes-cursor-keyboard-tab`

## 1. 요청
- 키보드 동작 확인됨. 이어서:
  1. **Shift+↑/↓** → 다중(범위) 선택.
  2. **→** → 폴더인 경우 확장(디스클로저 펼침과 동일).
  3. **←** → 폴더인 경우 접힘.

## 2. 구현
- **캐럿 분리**: 기존엔 anchor를 이동 커서로도 겸용 → Shift 범위가 불가. `_leftCaret`/`_rightCaret`(현재 위치)를 `_leftAnchor`/`_rightAnchor`(범위 고정점)와 분리.
- **`OnGridKeyDown` 재작성**:
  - 세로: 캐럿 기준 한 칸 이동. **Shift**면 고정 anchor~새 캐럿 범위 선택(연속 확장), 아니면 단일(anchor=caret=next).
  - 가로: 캐럿 행이 폴더면 **→ 펼침 / ← 접힘**(`SetExpanded`). 폴더 아니면 무시. 둘 다 `e.Handled`.
- **펼침/접힘 공용화**: `OnToggleExpand`의 본체를 **`SetExpanded(item, expand)`** 로 추출(클릭·키보드 공용, 이미 그 상태면 no-op).
- **클릭 연동**: `OnRowPointerPressed`에서 클릭 시 캐럿 갱신 → 이후 키보드가 클릭 지점부터 이어짐.

## 3. 검증
- `dotnet build app/Nexa.App -c Debug` → **0/0**.
- 수동: Shift+↓/↑ 범위 확장, 폴더에서 →/← 펼침·접힘.

## 4. 다음(백로그)
- ← 접힘 시 **부모로 점프**(트리 상위) 옵션, **Ctrl+↑/↓**(선택 유지 커서 이동)·**Ctrl+A**, Home/End·PageUp/Down.
- 이후: F15 후속(설정 JSON 영속화 + 보기 메뉴 토글) 또는 A5 정렬.
