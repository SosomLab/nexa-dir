# 작업 기록 — 2026-07-02 00:24 (KST) · Alt+↓ 항목 활성화 (F19)

> 기록 ID: `20260702_002427_alt-down-activate`
> 이전 기록: `20260702_001500_expansion-state-terminology`

## 1. 요구
- Alt+아래 화살표: 디렉터리면 진입(더블클릭), 파일이면 실행(확장자 연결).

## 2. 구현 (F19)
- `OnGridKeyDown`: `e.Key==Down && IsAltDown() && cur>=0` → `ActivateItem(활성패널, 캐럿항목)`, `e.Handled=true`.
- `ActivateItem(left, item)`:
  - 폴더/심볼릭 → `Navigate(left, FullPath, record:true)` (더블클릭과 동일, 뒤로 가능).
  - 파일 → `StorageFile.GetFileFromPathAsync` + `Launcher.LaunchFileAsync`(연결 프로그램). 실패는 상태바 격리.
- 활성 패널 기준(포커스 비의존). 문서 [19](19-implemented-features.md) F19.

## 3. 검증
- 맥 빌드 불가(WinUI) → push 후 CI(windows) app job green 확인.
- Windows 수동: 폴더 캐럿+Alt+↓ 진입 · 파일 캐럿+Alt+↓ 기본 프로그램 실행.

## 4. 후속
- Enter로도 활성화(옵션) · 더블클릭 시 파일도 실행(현재 폴더만).
