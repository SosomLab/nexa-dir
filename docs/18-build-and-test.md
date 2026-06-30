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
| WinUI 앱(C#) | `dotnet build app/Nexa.App` (cargo로 nexa-interop 자동 빌드 포함) | (현재 단위 없음) · `dotnet run` 수동 | **Windows 전용** |
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
# 전제: VS Build Tools 2022 + .NET SDK(8 또는 rollForward로 9+) + Windows App SDK 런타임 + cargo(PATH; 인터롭 빌드용)
dotnet restore app/Nexa.App
dotnet build   app/Nexa.App -c Debug           # 개발 빌드
dotnet build   app/Nexa.App -c Release --no-restore   # CI와 동일(릴리스)
dotnet run     --project app/Nexa.App          # 실행(수동 확인)
```

- `.NET SDK`는 `global.json`(8.0.0 + `rollForward: latestMajor`)을 따름 → **9.x SDK로도 빌드 가능**.
- 현재는 스켈레톤(빈 윈도우) — 단위 테스트 프로젝트는 아직 없음. 기능 단위 추가 시 `Nexa.*.Tests` 프로젝트와 `dotnet test` 절차를 본 문서에 추가.
- (권장 후속) 맥 빌드 가능한 `Nexa.ViewModels`(net8.0) 분리 시: `dotnet test app/Nexa.ViewModels.Tests` 절차 추가([11](11-dev-environment.md) §6-1).

### 2-1. Rust 코어 인터롭 통합 (cdylib 자동 빌드·복사)

`dotnet build app/Nexa.App`는 **cargo로 `nexa-interop`(cdylib)를 먼저 빌드**하고 산출 `nexa_interop.dll`을 앱 출력 디렉토리로 복사한다(csproj 타겟 `BuildNexaInterop` → `CopyNexaInterop`). C# **P/Invoke**([app/Nexa.App/NativeInterop.cs](../app/Nexa.App/NativeInterop.cs))가 런타임에 이 dll을 로드해 코어 함수를 호출한다(왕복 PoC).

- **전제**: `cargo`가 PATH에 있어야 한다(bootstrap.ps1이 처리). 없으면 빌드 중 cargo 실행 단계에서 실패.
- **프로필 매핑**: dotnet `Debug` → `cargo build`(`core/target/debug`), `Release` → `cargo build --release`(`core/target/release`).
- **산출물**: `app/Nexa.App/bin/<plat>/<cfg>/.../nexa_interop.dll`.
- **의존성**: 따라서 **앱 빌드는 코어(Rust) 빌드에 의존** → CI app job에도 Rust 툴체인이 필요(§4).
- **검증(dll 단독 P/Invoke)**: 빌드 후 PowerShell에서 왕복을 직접 확인 가능 —
  ```powershell
  Add-Type @"
  using System.Runtime.InteropServices;
  public static class T { [DllImport("nexa_interop", CallingConvention=CallingConvention.Cdecl)]
    public static extern int nexa_poc_add(int a, int b); }
  "@
  Push-Location core\target\debug; [T]::nexa_poc_add(2,3); Pop-Location   # → 5
  ```

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
| `app` | windows-latest | Rust 툴체인 설치(인터롭 빌드용) → `dotnet restore` → `dotnet build app/Nexa.App -c Release --no-restore`(cargo로 nexa-interop 자동 빌드 포함) |

- 트리거: `main` push · 모든 PR. main은 항상 green 유지.
- 후속: MSIX 패키징 + 서명(secrets) + 포터블 산출 → Releases 업로드([12](12-packaging-portable.md)).

## 5. tools/ — (예정, M7)

`tools/nexa-license-gen`(라이선스 키 생성 CLI) 추가 시: `cargo build -p nexa-license-gen` / `cargo test` 절차를 본 문서에 추가. 비밀키는 저장소 밖([17](17-licensing-activation.md) §5-1).

## 5-1. 디버깅 (브레이크포인트 / step / watch)

> rust-analyzer의 **"▶ Debug" CodeLens**(테스트/`fn main` 위)로 디버그. **디버그 빌드에서만** 라인이 정확히 바인딩됨(release는 인라인 최적화로 어긋남).
> ⚠️ **디버거 엔진은 OS의 디버그 정보 형식에 맞춰야 한다 — 공유 설정에 고정 금지.**

| OS | 디버그 정보 | 디버거(확장) | `rust-analyzer.debug.engine` |
| --- | --- | --- | --- |
| **Windows (MSVC)** | **PDB** | C/C++ — **cppvsdbg** (`ms-vscode.cpptools`) | `ms-vscode.cpptools` |
| **macOS / Linux** | **DWARF** | **CodeLLDB** (`vadimcn.vscode-lldb`) | `vadimcn.vscode-lldb` (또는 auto) |

- **왜 OS별인가**: `cppvsdbg`는 **Windows 전용**(PDB). `CodeLLDB`(LLDB)는 **DWARF**용(mac/Linux). Windows MSVC를 LLDB로 디버그하면 **브레이크/watch가 안 잡히고**, 반대로 cppvsdbg는 mac/Linux에 없다.
- **설정 위치**: `.vscode/settings.json`(저장소 공유)에는 엔진을 **고정하지 않는다**(기본 auto). 각자 **VSCode User Settings**에 위 값을 지정. (auto는 CodeLLDB를 우선 선택하므로, CodeLLDB도 설치된 Windows에선 `ms-vscode.cpptools`를 **명시**해야 함)
- **확장 설치**: Windows=cpptools, macOS/Linux=CodeLLDB. [.vscode/extensions.json](../.vscode/extensions.json)이 둘 다 추천 — **자기 OS 것만** 설치하면 auto가 알맞게 고름.
- **`const` watch 한계**: Rust `const`(예 `CORE_VERSION = env!("CARGO_PKG_VERSION")`)는 컴파일타임에 인라인되어 **런타임 심볼이 없다** → watch에서 `identifier undefined`. 값 관찰이 필요하면 `let v = CORE_VERSION;`처럼 **지역 변수로 바인딩**해서 본다. 또 cppvsdbg는 C++ 식 평가기라 `.is_empty()` 같은 **Rust 메서드 호출은 watch 평가 불가**(변수만 넣을 것).

## 6. 실행 & 동작 확인 (Run / smoke test)

빌드가 끝나면 실제로 실행해 동작을 확인한다.

### 6-1. 코어 — 자동 테스트
```bash
cd core
cargo test --workspace          # 단위 테스트(현재 5개: nexa-core 2 · nexa-interop 2 · nexa-vfs 1)
cargo test -p nexa-interop      # 특정 크레이트만
```

### 6-2. WinUI 앱 — 실행(수동 스모크) · Windows
```powershell
dotnet run --project app/Nexa.App
```
- 창 가운데 상태줄에 **인터롭 왕복 결과**가 표시되면 성공:
  `인터롭 OK — abi=1, nexa_poc_add(2, 3)=5`
- `인터롭 실패: ...` 가 보이면 dll 미복사/미로드 → §2-1(빌드 통합)·`cargo`가 PATH인지 확인.
- 종료: 창 닫기. (현재는 빈 셸 + PoC 표시 단계 — 후속 단위에서 경로바·트리 추가)

### 6-3. 인터롭 dll 단독 왕복 — 헤드리스(GUI 없이)
GUI를 띄우지 않고 `nexa_interop.dll`만으로 P/Invoke 왕복을 검증(§2-1 스니펫):
```powershell
# 앱을 한 번 빌드해 dll이 출력에 있는 상태에서
$out = (Resolve-Path "app\Nexa.App\bin\Debug\net8.0-windows10.0.19041.0").Path
$env:Path = "$out;$env:Path"; [Environment]::CurrentDirectory = $out
Add-Type @"
using System.Runtime.InteropServices;
public static class T { [DllImport("nexa_interop", CallingConvention=CallingConvention.Cdecl)]
  public static extern int nexa_poc_add(int a, int b); }
"@
[T]::nexa_poc_add(2,3)    # → 5
```
> 주의: .NET native 로드는 PWD가 아니라 `Environment.CurrentDirectory`/PATH 기준 → 위처럼 절대경로 지정.

### 6-4. 라이선스 게이트 — 실행
```bash
cargo install cargo-deny     # 최초 1회(미설치 시 'no such command: deny')
cargo deny --manifest-path core/Cargo.toml check licenses bans advisories
```
- `advisories ok, bans ok, licenses ok` 면 통과(exit 0).
- `license-not-encountered` **경고**는 허용 목록 라이선스가 아직 의존성에 안 쓰인 것 → **정상**(의존성 추가 시 사라짐).

## 7. 빠른 전체 점검 (로컬 머지 전)

```bash
# 코어 (mac/Win/Linux)
cd core && cargo fmt --all --check && cargo clippy --workspace --all-targets -- -D warnings && cargo test --workspace && cd ..
# 라이선스
cargo deny --manifest-path core/Cargo.toml check licenses bans advisories
# 앱 (Windows만)
dotnet build app/Nexa.App -c Release
```
