# 작업 기록 — 2026-07-01 23:57:49 (KST) · ←로 상위 폴더 이동

> 기록 ID: `20260701_235749_left-key-parent-nav`
> 이전 기록: `20260701_234755_progress-review-analysis`

## 1. 요구
- 인라인 트리에서 `←` 키: 최상위 폴더(부모 없음)면 무동작, 파일/폴더에서는 **상위(부모) 폴더 행으로 이동**
  (예: `.vscode` → `nexa-dir`). 표준 트리 동작.

## 2. 구현 (F17-1)
- `OnGridKeyDown`의 `←` 분기 재구성:
  - 펼쳐진 폴더 → **접기**(기존 유지).
  - 접힌 폴더/파일 → **부모 행으로 이동**(단일 선택 + 캐럿 + BringIntoView). 부모 없으면(Depth 0) 무동작.
- 헬퍼 `ParentIndex(list, index)`: 현재보다 `Depth`가 1 작은 최근접 상위 행 인덱스(없으면 -1).
- `→`는 기존대로(폴더 펼침).
- 구현: [app/Nexa.App/MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) · 문서 [19](19-implemented-features.md) F17-1.

## 3. 검증
- 맥 빌드 불가(WinUI) → **push 후 CI(windows) `app` job green 확인 필수**.
- Windows 수동: 자식 행에서 `←` → 부모 이동·선택. 최상위에서 `←` → 변화 없음. 펼친 폴더에서 `←` → 접힘.

## 4. 후속(백로그)
- `→`를 펼친 폴더에서 누르면 첫 자식으로 이동(대칭) · Home/End/PageUp·Down · Ctrl+A.
