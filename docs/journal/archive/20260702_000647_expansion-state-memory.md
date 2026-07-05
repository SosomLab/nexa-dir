# 작업 기록 — 2026-07-02 00:06:47 (KST) · 펼침 상태 기억 & 진입 복원 (F18)

> 기록 ID: `20260702_000647_expansion-state-memory`
> 이전 기록: `20260701_235749_left-key-parent-nav`

## 1. 요구
- 현재 폴더 하위에 **열려 있는(펼친) 폴더 상태를 저장**했다가, 그 폴더로 **진입하면 동일한 상태로** 표시.
- 예: A에서 A2 펼침(A21/A22/A23) + A22 펼침(A221/A222/A223) 상태 → A2 진입 시 그대로 복원.

## 2. 기능 정의 (F18, FR-X4 일부)
- **경로 기준·패널별** 펼침 기억. 진입/이동(더블클릭·뒤로/앞으로/위로) 시 하위 열린 폴더 재귀 복원. 재펼침도 sticky.
- 접힘 = 그 폴더만 기억 제거(자손 상태 유지) → 다시 펼치면 하위까지 복원.

## 3. 구현
- `HashSet<string> _leftExpanded/_rightExpanded`(OrdinalIgnoreCase) — 펼친 폴더 경로.
- `SetExpanded` 리팩터: 기억 추가/제거 + `ExpandInPlace`/`CollapseInPlace`(삽입/제거) 분리 + `ApplySavedExpansion`(재귀 복원).
- `LoadDirectory`: 직접 자식 로드 후 `ApplySavedExpansion` 호출(헤더 항목수=직접 자식 기준).
- 재귀 복원 원리: 펼침이 자식을 바로 아래 삽입 → 인덱스 루프가 삽입된 자식도 방문 → 임의 깊이 복원.
- 문서: [19](19-implemented-features.md) F18.

## 4. 검증
- 맥 빌드 불가(WinUI) → **push 후 CI(windows) app job green 확인**.
- Windows 수동: 다단 펼침 후 진입/위로/재펼침 시 하위 상태 동일 표시.

## 5. 한계/후속
- 세션 영속화(JSON) 미포함(인메모리) · 외부 변경 반영 · 코어 `VisibleRow` 스트림(C1) 이관 시 통합.
