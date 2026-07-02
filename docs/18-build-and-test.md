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

## 0-1. 빠른 시작 — 코어 빌드 → dll 복사 → 앱 빌드 → 실행 (Windows)

> ⭐ **dll 복사는 수동으로 할 필요가 없다.** `dotnet build app/Nexa.App`가 **① cargo로 코어(nexa-interop) 빌드 → ② `nexa_interop.dll`을 앱 출력에 복사 → ③ 앱 빌드**를 **한 번에** 수행한다(csproj 타겟, §2-1). 그래서 실제 순서는 아래 2줄이면 끝.

```powershell
# (최초 1회) 환경: scripts/bootstrap.ps1  ·  앱 실행 런타임: Windows App Runtime 1.6 (§6-2)
dotnet build app/Nexa.App -c Debug     # ①코어(cargo)+②dll 복사+③앱 빌드 (자동, 한 번에)
dotnet run   --project app/Nexa.App    # ④실행 → 창에 "인터롭 OK — abi=2, nexa_poc_add(2, 3)=5"
```

- 단계별 상세: 코어 **[§1]** · dll 자동 복사 **[§2-1]** · 앱 빌드 **[§2]** · 실행 **[§6-2]** · dll 잠금 오류 **[§6-3]**.
- **코어만 따로(수동)** 빌드/복사하고 싶을 때(보통 불필요 — 위 `dotnet build`가 최신으로 재수행):
  ```powershell
  cd core; cargo build; cd ..                                   # → core/target/debug/nexa_interop.dll 생성
  Copy-Item core\target\debug\nexa_interop.dll `
    app\Nexa.App\bin\Debug\net8.0-windows10.0.22621.0\          # (선택) 수동 복사
  ```
- **Release**는 `-c Release`(코어도 `cargo build --release` → `core/target/release`에서 복사, §2-1).
- **macOS 호스트**: 앱 빌드·실행 불가(§2 ⚠️) → 코어(§1)만. 앱은 Windows/VM/CI.

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

**Windows 전용**(XAML 컴파일러가 Windows 네이티브). TFM `net8.0-windows10.0.22621.0`, 비패키지(unpackaged) 구성.

> ⚠️ **macOS/Linux에서 `dotnet build/run app/Nexa.App` 실행 금지** — 즉시 다음 오류:
> ```
> error NETSDK1100: 이 운영 체제에서 Windows를 대상으로 하는 프로젝트를 빌드하려면
>                   EnableWindowsTargeting 속성을 true로 설정합니다.
> ```
> **`EnableWindowsTargeting=true`로 우회하지 말 것** — 그 다음 단계에서 `XamlCompiler.exe`(Windows 전용)로 실패한다([11 §6-1](11-dev-environment.md)).
> 앱 빌드·실행은 **Windows / Windows VM / CI**에서만 — 맥 호스트는 VM([11 §4-4](11-dev-environment.md)). 맥에서는 **코어(Rust)만**(§1) 빌드.

```powershell
# Windows 전제: VS Build Tools 2022 + .NET SDK(8 또는 rollForward로 9+) + Windows App SDK 런타임 + cargo(PATH; 인터롭 빌드용)
dotnet restore app/Nexa.App
dotnet build   app/Nexa.App -c Debug           # 개발 빌드 (Windows 전용)
dotnet build   app/Nexa.App -c Release --no-restore   # CI와 동일(릴리스)
dotnet run     --project app/Nexa.App          # 실행(수동 확인)
```

- `.NET SDK`는 `global.json`(8.0.0 + `rollForward: latestMajor`)을 따름 → **9.x SDK로도 빌드 가능**.
- ⚠️ **TFM 정합 규칙**: 앱 `TargetFramework`의 Windows 버전(현재 **22621**)은 **참조하는 UI 패키지(CommunityToolkit 등)가 제공하는 TFM과 일치**시켜야 한다. 불일치 시 복원은 되어도 **XAML 컴파일에서 CS0234**(`GridSplitter` 등 미해결) → `XamlCompiler` MSB3073. 확인: `Get-ChildItem <pkg>/lib`.
  - `TargetFramework`(빌드 SDK) ≠ `TargetPlatformMinVersion`(실행 최소, 17763) — **역할이 다르며 값이 달라도 정상**.
- ⚠️ **macOS에서 앱을 바꿨으면 CI 필수 확인**: WinUI는 맥에서 **빌드 불가** → 로컬 통과가 없다. push 후 **CI(windows) `app` job green을 반드시 확인**(`gh run watch`)해야 한다. 이 검증을 건너뛰면 빌드 깨짐이 여러 커밋 누적된다(실제 사례: 레이아웃 골격 3커밋이 TFM 불일치로 CI 연속 실패).
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

> rust-analyzer의 **"▶ Debug" CodeLens**(테스트/`fn main` 위) 또는 **F5([.vscode/launch.json](../.vscode/launch.json))** 로 디버그.
> **디버그 빌드에서만** 라인이 정확히 바인딩됨(release는 인라인 최적화로 어긋남).
> ⚠️ **디버거 엔진은 OS의 디버그 정보 형식에 맞춰야 한다 — 공유 설정에 고정 금지.**
>
> ✅ **macOS 검증(2026-06-30)**: `lldb`로 브레이크포인트 바인딩·적중 확인
> (`Breakpoint 1: where = nexa_poc_add …`, `stop reason = breakpoint`). `lldb`+`rust-lldb` 설치됨, cdylib(`libnexa_interop.dylib`) 빌드 OK.
> → 맥에서 **F5 "Rust: 코어 테스트 디버그 (macOS/Linux · CodeLLDB)"** 로 원클릭 디버그. (CodeLLDB의 `cargo` 통합이 테스트 바이너리 자동 빌드/탐색)

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

> **macOS 호스트**: 앱은 맥에서 **빌드도 실행도 불가**(§5-1 주의/[11 §6-1](11-dev-environment.md)). 맥에서 실행 테스트는
> **Windows VM**(Parallels/UTM/VMware) 또는 물리 PC/CI — 방법 [11 §4-4](11-dev-environment.md).
>
> **전제(최초 1회, Windows)**: **Windows App Runtime 1.6** 설치 — unpackaged 앱은 실행 시 시스템 런타임을 요구한다.
> winget `Microsoft.WindowsAppRuntime.1.6`은 **framework만** 깔아 Main/DDLM/Singleton이 빠지므로 부족 →
> **공식 인스톨러** `windowsappruntimeinstall-x64.exe`(<https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe>)로
> 전체 세트 설치(bootstrap.ps1이 처리). 미설치 시 "requires Windows App Runtime 1.6 (>= 6000.519.329.0)" 대화상자.
> (배포 시엔 self-contained 번들로 런타임 의존 제거 가능 — [12](12-packaging-portable.md))

```powershell
dotnet run --project app/Nexa.App
```
- 창 가운데 상태줄에 **인터롭 왕복 결과**가 표시되면 성공:
  `인터롭 OK — abi=2, nexa_poc_add(2, 3)=5`
- `인터롭 실패: ...` 가 보이면 dll 미복사/미로드 → §2-1(빌드 통합)·`cargo`가 PATH인지 확인.
- 종료: 창 닫기. (현재는 빈 셸 + PoC 표시 단계 — 후속 단위에서 경로바·트리 추가)

### 6-3. 인터롭 dll 단독 왕복 — 헤드리스(GUI 없이)
GUI를 띄우지 않고 `nexa_interop.dll`만으로 P/Invoke 왕복을 검증(§2-1 스니펫):
```powershell
# 앱을 한 번 빌드해 dll이 출력에 있는 상태에서
$out = (Resolve-Path "app\Nexa.App\bin\Debug\net8.0-windows10.0.22621.0").Path
$env:Path = "$out;$env:Path"; [Environment]::CurrentDirectory = $out
Add-Type @"
using System.Runtime.InteropServices;
public static class T { [DllImport("nexa_interop", CallingConvention=CallingConvention.Cdecl)]
  public static extern int nexa_poc_add(int a, int b); }
"@
[T]::nexa_poc_add(2,3)    # → 5
```
> 주의: .NET native 로드는 PWD가 아니라 `Environment.CurrentDirectory`/PATH 기준 → 위처럼 절대경로 지정.
> ⚠️ **dll 잠금**: 이 검증은 `nexa_interop.dll`을 **LoadLibrary로 잠근다**(프로세스 종료 전까지 언로드 안 됨).
> 검증에 쓴 PowerShell 세션을 **닫지 않으면** 다음 `dotnet build/run`의 dll 복사가 실패한다
> (`MSB3026: ... being used by another process`). → 검증 후 그 세션을 종료하거나 **일회용 세션**(`pwsh -Command ...`)에서 실행.

#### dll 잠금 해제 (MSB3026 "다른 프로세스가 사용 중" 발생 시)

오류 예: `warning MSB3026: ... nexa_interop.dll ... 파일이 "PowerShell 7 (12820)"에 의해 잠겨 있습니다.`

```powershell
# 1) 오류 메시지에 잠근 PID가 보이면 그 PID를 바로 종료 (위 예 = 12820)
Stop-Process -Id 12820 -Force

# 2) PID를 모를 때 — nexa_interop.dll을 모듈로 잡은 프로세스를 검색해 종료
$abs = (Resolve-Path "app\Nexa.App\bin\Debug\net8.0-windows10.0.22621.0\nexa_interop.dll").Path
$locking = Get-Process pwsh, powershell, dotnet, Nexa.App -ErrorAction SilentlyContinue |
  Where-Object { try { ($_.Modules | Where-Object FileName -eq $abs).Count -gt 0 } catch { $false } }
$locking | Select-Object Id, ProcessName, StartTime   # 무엇이 잡았는지 확인
$locking | Stop-Process -Force                        # 종료

# 3) 해제 확인 — 코어 산출 dll을 출력에 덮어써서 복사가 되면 OK
Copy-Item (Resolve-Path "core\target\debug\nexa_interop.dll") $abs -Force
```

- 보통 헤드리스 검증(§6-3 1·2번)에 쓴 PowerShell이 범인이다 — 종료하면 즉시 해제된다.
- VSCode 통합 터미널에서 검증했다면 **그 터미널 탭**이 dll을 잡고 있을 수 있으니 탭을 닫거나 위 절차로 종료.
- 재발 방지: 검증은 `pwsh -Command "..."`(일회용)로 돌려 명령 종료와 함께 dll이 풀리게 한다.

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
