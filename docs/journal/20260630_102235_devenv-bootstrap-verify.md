# 작업 기록 — 2026-06-30 10:22:35 (KST)

> 기록 ID: `20260630_102235_devenv-bootstrap-verify`
> 이전 기록: `20260630_021738_status-sync`

## 1. 요구
- 설계에 필요한 문서/메모리 분석·정리.
- **이 Windows PC(KIROS33-GALAXYB)에서 개발 가능하도록** 개발환경 점검 + 없으면 설치.

## 2. 설계 분석 정리
- 스택(DR-1): Rust 코어(cdylib, 핫패스) + WinUI 3(C#/.NET8), 인터롭 C ABI/csbindgen.
- 플래그십(M1): 인라인 폴더 확장 + 폴더 교차 다중 선택 → "가시 노드 평면 스트림(VisibleRow)" + 가상화.
  성능 NFR-P1/P2: 10만 노드 첫 렌더 <150ms, 60fps. (docs/07)
- 단위 백로그 M0→M1: docs/15 §7. 현황: STATUS.md. 상세 근거: docs/01·07·16.
- 휴대용 메모리(.claude, 저장소 밖)에 project-overview / dev-env-windows-pc / dev-methodology-units 기록.

## 3. 개발환경 점검 → 설치 → 검증
- 점검 전: Git/winget/choco ✅ · .NET 런타임(WindowsDesktop 9/10)만 있음 · **Rust 없음 · .NET SDK 없음 · VS Build Tools 없음**.
- 설치(`scripts/bootstrap.ps1`, 관리자/choco, 전체):
  - Rust 1.96.0 (rustup/cargo) + target x86_64-pc-windows-msvc ✅
  - VS Build Tools 2022 (VCTools + ManagedDesktopBuildTools 워크로드) ✅
  - .NET SDK 9.0.315 (global.json 8.0.0 + rollForward latestMajor 로 호환) ✅
- **실빌드 검증**:
  - `cargo test --manifest-path core/Cargo.toml` → 4 tests green (MSVC 링커 동작 확인).
  - `dotnet build app/Nexa.App -c Debug` → 경고 0 / 오류 0, Nexa.App.dll(net8.0-windows10.0.19041.0) 생성.
  - 결론: 이 PC에서 **코어 + WinUI 앱 풀 빌드 가능** 확인.

## 4. 부수 수정 (구현 단위)
- `scripts/bootstrap.ps1`: .NET SDK 감지를 `dotnet --list-sdks` 기준으로 개선.
  - 기존엔 `Get-Command dotnet` 만 봐서 **런타임만 있어도 '설치됨'으로 오판** → SDK 미설치 PC에서 SDK 설치를 건너뛰는 버그.

## 5. 다음
- M0 기능 단위 착수: 인터롭 PoC(Rust↔C# 왕복) → nexa-vfs 로컬 스트리밍 열거 → 가상화 렌더.
