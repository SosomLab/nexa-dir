# 18 · 빌드 & 테스트 가이드 (부문별)

> **이 문서는 부문별 빌드·테스트 절차의 단일 출처(SSOT)다.**
> ⚠️ **갱신 규약**: 빌드/테스트 **명령·도구·전제·산출물·OS 지원이 바뀌는 변경 이슈가 생길 때마다 이 문서를 같은 커밋에서 갱신**한다.
> (예: 새 크레이트/프로젝트 추가, 타깃 TFM 변경, 패키징 추가, CI 스텝 변경, 의존 도구 버전 고정 등)
> 신뢰 원천은 **CI**([.github/workflows/ci.yml](../.github/workflows/ci.yml)) — 로컬 명령은 CI와 일치시킨다.
> 관련: 환경 구성 [11](11-dev-environment.md) · 구조 [16](16-project-structure.md) · 포터블/패키징 [12](12-packaging-portable.md) · 현황 [STATUS](STATUS.md).

---

## 0. 한눈에 (요약)

| 부문 | 빌드 | 테스트/검사 | OS |
| --- | --- | --- | --- |
| 코어(Rust) | `cargo build` | `cargo test --workspace` · `fmt` · `clippy` | mac/Win/Linux |
| WinUI 앱(C#) | `dotnet build app/Nexa.App` | (현재 단위 없음) · `dotnet run` 수동 | **Windows 전용** |
| 라이선스 게이트 | — | `cargo deny check ...` | any |
| 패키징(MSIX/포터블) | (후속, [12](12-packaging-portable.md)) | — | Windows |

전제: 환경 미구성 시 먼저 부트스트랩 — mac `bash scripts/bootstrap.sh`, Windows(관리자) `scripts/bootstrap.ps1`. 상세 [11](11-dev-environment.md).

---

## 1. 코어 — Rust 워크스페이스 (`core/`)

맥/Windows/Linux 모두 빌드·테스트 가능(일상 개발은 맥 권장). 멤버: `nexa-core`, `nexa-vfs`, `nexa-interop`(cdylib).

```bash
# 작업 디렉터리 = core/ (또는 --manifest-path core/Cargo.toml)
cd core

cargo build                                    # 디버그 빌드
cargo build --release                          # 릴리스(LTO thin, opt 3) — cdylib 산출
cargo test --workspace                         # 전체 테스트
cargo test -p nexa-vfs                          # 특정 크레이트만

# CI와 동일한 게이트(머지 전 로컬 권장)
cargo fmt --all --check                        # 포맷 검사
cargo clippy --workspace --all-targets -- -D warnings   # 린트(경고=실패)
```

- Windows에서 빌드하려면 **VS Build Tools(MSVC 링커)** + target `x86_64-pc-windows-msvc` 필요(부트스트랩이 처리).
- Windows 전용 코드는 `#[cfg(windows)]`로 격리 — 맥에서는 stub로 컴파일.
- `core/` 루트 외에서 실행 시 모든 명령에 `--manifest-path core/Cargo.toml` 부가.

## 2. WinUI 앱 — C#/.NET (`app/Nexa.App`)

**Windows 전용**(XAML 컴파일러가 Windows 네이티브). TFM `net8.0-windows10.0.19041.0`, 비패키지(unpackaged) 구성.

```powershell
# 전제: VS Build Tools 2022 + .NET SDK(8 또는 rollForward로 9+) + Windows App SDK 런타임
dotnet restore app/Nexa.App
dotnet build   app/Nexa.App -c Debug           # 개발 빌드
dotnet build   app/Nexa.App -c Release --no-restore   # CI와 동일(릴리스)
dotnet run     --project app/Nexa.App          # 실행(수동 확인)
```

- `.NET SDK`는 `global.json`(8.0.0 + `rollForward: latestMajor`)을 따름 → **9.x SDK로도 빌드 가능**.
- 현재는 스켈레톤(빈 윈도우) — 단위 테스트 프로젝트는 아직 없음. 기능 단위 추가 시 `Nexa.*.Tests` 프로젝트와 `dotnet test` 절차를 본 문서에 추가.
- (권장 후속) 맥 빌드 가능한 `Nexa.ViewModels`(net8.0) 분리 시: `dotnet test app/Nexa.ViewModels.Tests` 절차 추가([11](11-dev-environment.md) §6-1).

## 3. 라이선스 게이트 — cargo-deny

퍼미시브(MIT/Apache/BSD/ISC/MPL-2.0) 온리 강제. 설정 [core/deny.toml](../core/deny.toml).

```bash
cargo install cargo-deny      # 최초 1회
cargo deny --manifest-path core/Cargo.toml check licenses bans advisories
```

## 4. CI — 신뢰 원천 ([.github/workflows/ci.yml](../.github/workflows/ci.yml))

| Job | 러너 | 수행 |
| --- | --- | --- |
| `core` | macos-latest, windows-latest | `cargo fmt --check` → `clippy -D warnings` → `cargo test --workspace` |
| `license-gate` | ubuntu-latest | `cargo deny check licenses bans advisories` |
| `app` | windows-latest | `dotnet restore` → `dotnet build app/Nexa.App -c Release --no-restore` |

- 트리거: `main` push · 모든 PR. main은 항상 green 유지.
- 후속: MSIX 패키징 + 서명(secrets) + 포터블 산출 → Releases 업로드([12](12-packaging-portable.md)).

## 5. tools/ — (예정, M7)

`tools/nexa-license-gen`(라이선스 키 생성 CLI) 추가 시: `cargo build -p nexa-license-gen` / `cargo test` 절차를 본 문서에 추가. 비밀키는 저장소 밖([17](17-licensing-activation.md) §5-1).

## 6. 빠른 전체 점검 (로컬 머지 전)

```bash
# 코어 (mac/Win/Linux)
cd core && cargo fmt --all --check && cargo clippy --workspace --all-targets -- -D warnings && cargo test --workspace && cd ..
# 라이선스
cargo deny --manifest-path core/Cargo.toml check licenses bans advisories
# 앱 (Windows만)
dotnet build app/Nexa.App -c Release
```
