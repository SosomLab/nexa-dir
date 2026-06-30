# 작업 기록 — 2026-06-30 23:59:59 (KST)

> 기록 ID: `20260630_235959_macos-debug-and-vm`
> 이전 기록: `20260630_182543_column-system`

## 1. 요구
- 저장소 최신화(원격 25커밋 fast-forward 완료).
- macOS에서 **Rust 소스 디버그**가 잘 되는지 확인.
- macOS에서 **dotnet 앱 실행 테스트** 가능 여부 + 불가 시 **Windows VM 방법** 문서 최신화.

## 2. 확인 결과 (실측)
- **Rust on macOS = 정상**: `cargo test --workspace` green(9 tests), `cargo build` cdylib `libnexa_interop.dylib` OK.
- **디버그 도구**: `lldb-2100`, `rust-lldb` 설치됨. lldb 배치로 **브레이크포인트 바인딩·적중 확인**
  (`Breakpoint 1: where = nexa_poc_add …`, `stop reason = breakpoint`). → 맥 Rust 소스 디버그 OK.
- **WinUI 앱 on macOS = 빌드·실행 불가**(XAML 컴파일러 Windows 전용, §6-1). 실행 테스트는 Windows VM/PC/CI 필요.

## 3. 반영 (Done)
- `.vscode/launch.json` 추가 — F5 "Rust: 코어 테스트 디버그(macOS/Linux · CodeLLDB)" (cargo 통합 자동 빌드/탐색).
- `docs/18 §5-1`: macOS 디버그 검증 결과 + launch.json/F5 안내.
- `docs/18 §6-2`: macOS 호스트는 빌드·실행 불가 → Windows VM 안내(11 §4-4 링크).
- `docs/11 §4-4`: "macOS에서 Windows 앱 실행/테스트 — Windows VM" 전체 가이드
  (Apple Silicon=Win11 ARM64 Parallels/UTM/VMware, Intel=x64, ARM64 네이티브 빌드 권장, 절차/주의/성능 측정은 물리 PC).
- `docs/11 §6-1`, `docs/16`(.vscode/launch.json) 동기화.

## 4. 다음
- 단위 백로그(docs/15 §7) 계속: M0 #6 네비게이션 확장 또는 M1 경로 바/트리. 맥=코어·디버그, 앱 실행 확인=VM.
