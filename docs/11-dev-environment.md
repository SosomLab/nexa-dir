# 11 · 개발 환경 구성 가이드

> "맥에서 개발 가능하면 좋고, Windows가 필요하면 환경 구성을 먼저 안내" + "다른 Windows PC에서도
> 저장소 기반으로 개발/테스트" 요구 반영. 스택은 **안 A(Rust 코어 + WinUI 3)** 기준.

---

## 1. 핵심 원칙 — 무엇을 어디서 빌드하나

| 구성요소 | macOS | Windows | 설명 |
| --- | :---: | :---: | --- |
| `core/` (Rust) | ✅ 빌드·테스트 | ✅ | 엔진(VFS·인덱스·프리뷰·ops·plugin 호스트). **일상 개발은 맥에서** |
| `core/` 의 Windows 전용부 | ⚠️ 컴파일만(stub) | ✅ 실동작 | 셸 COM·ConPTY는 `#[cfg(windows)]` 격리, 맥은 trait/stub |
| `app/` (WinUI 3) | ❌ | ✅ 빌드·실행 | UI/통합/패키징은 **Windows 필수** |
| MSIX/포터블 패키징 | ❌ | ✅ | Windows 또는 CI(windows-latest) |

> **설계 원칙:** OS 비의존 코어를 최대화하고 WinUI 셸을 얇게 → 개발의 대부분을 맥에서 수행,
> Windows는 UI/통합/패키징에만 필요.

## 2. 권장 워크플로우

- **맥(이 저장소 기본 머신):** 코어(Rust) 개발·`cargo test`·벤치·문서. VSCode + rust-analyzer.
- **Windows PC/VM:** 앱(WinUI) 빌드·실행·수동 테스트·MSIX/포터블 패키징.
- **CI(GitHub Actions, windows-latest):** 매 푸시 빌드·테스트·아티팩트(MSIX·포터블) 산출 →
  맥 단독 작업 시에도 실행 산출물 확보, **다른 PC 없이도 검증** 가능.

```
[macOS] 코어 개발 ─push─▶ GitHub ─▶ Actions(windows-latest) ─▶ MSIX/포터블 아티팩트
                          │
                  [Windows PC/VM] clone ─▶ bootstrap.ps1 ─▶ 앱 빌드·실행·테스트
```

## 3. macOS 환경 (코어 개발) — Homebrew

```bash
git clone https://github.com/SosomLab/nexa-dir.git && cd nexa-dir
bash scripts/bootstrap.sh        # brew로 git/rustup/dotnet-sdk/vscode 설치 + Rust 초기화
cargo test --manifest-path core/Cargo.toml   # 코어 빌드·테스트(맥 가능)
```
- **brew 미설치 시**: 스크립트가 Homebrew 설치 명령(<https://brew.sh>)을 안내한다.
- **brew에 패키지가 없거나 실패 시**: 스크립트가 수동 다운로드 URL을 안내(§4-5 표).
- VSCode 확장: **rust-analyzer**, CodeLLDB. 앱(WinUI)은 맥 빌드 불가(§6-1) — CI/Windows에서 확인.

## 4. Windows 환경 (전체 앱) — 저장소 기반 재현

### 4-1. 필요 도구
| 도구 | 용도 |
| --- | --- |
| Git | 저장소 |
| **Rust(rustup) + MSVC target** | 코어 빌드(`x86_64-pc-windows-msvc`) |
| **VS Build Tools 2022** (MSVC v143, Windows 11 SDK, .NET desktop) | Rust 링커 + WinUI 네이티브 + 패키징 도구. **풀 VS IDE는 선택** |
| **.NET 8 SDK** | WinUI 앱 빌드 |
| (선택) **Visual Studio 2022 Community** | XAML 디자이너·핫리로드 필요 시 편의 |
| VSCode + C# Dev Kit | 비IDE 개발 |
| Windows Terminal / winget | 설치·실행 |
| **C/C++ (`ms-vscode.cpptools`)** | Windows MSVC Rust **디버깅**(cppvsdbg) — [18](18-build-and-test.md) §5-1 |
| **Windows App Runtime 1.6** | unpackaged **앱 실행**(공식 인스톨러; winget framework만으론 부족) — [18](18-build-and-test.md) §6-2 |
| (선택) **cargo-deny** | 라이선스 게이트 **로컬 실행**(`cargo install cargo-deny`) — [18](18-build-and-test.md) §6-4 |

### 4-2. 부트스트랩 스크립트 (다른 Windows PC도 동일 환경)
> 관리자 PowerShell에서 `scripts/bootstrap.ps1` 1회 실행.
> **우선순위: Chocolatey(choco) → winget → (둘 다 실패 시) 수동 다운로드 안내.**

```powershell
git clone https://github.com/SosomLab/nexa-dir.git ; cd nexa-dir
# 관리자 PowerShell:
powershell -ExecutionPolicy Bypass -File scripts/bootstrap.ps1
```
- choco/winget가 모두 없으면 스크립트가 **Chocolatey 설치 명령**(또는 winget 안내)을 먼저 출력한다.
- 각 도구는 choco로 먼저 시도, 실패 시 winget, 그래도 안 되면 **수동 다운로드 URL**(§4-5 표)을 안내한다.

### 4-3. 빌드·실행
> ⚠️ 아래는 **Windows 전용**. 맥에서 `dotnet build/run app/Nexa.App` 실행 시 **`NETSDK1100` 오류**(이어 XamlCompiler 실패) — §6-1.
```powershell
git clone https://github.com/SosomLab/nexa-dir.git ; cd nexa-dir
cargo build --release                      # 코어 cdylib (맥/Windows 공통)
dotnet build app/Nexa.App -c Debug         # WinUI 앱 — Windows 전용
dotnet run  --project app/Nexa.App         # 실행 — Windows 전용
# 패키징은 §06/§12 참조 (MSIX / 포터블)
```

### 4-4. macOS에서 Windows 앱 실행/테스트 — Windows VM (또는 PC/CI)

> 질문: "macOS에서 dotnet 앱 **실행 테스트**도 되나?" → **아니오.** WinUI 3는 맥에서 **빌드도 실행도 불가**(§6-1).
> 맥에서 앱 동작을 보려면 **Windows 환경(VM/물리 PC/CI)** 이 필요하다. 아래는 **맥 호스트에 Windows VM**으로 실행하는 방법.

**실행 환경 선택지**

| 방법 | 비고 |
| --- | --- |
| **Windows VM (맥 로컬, 권장)** | 맥 위 Windows 11 → `bootstrap.ps1` → `dotnet run`. 가장 접근성 좋음 |
| 물리 Windows PC | 가장 정확 — **성능(NFR-P1/P2) 측정은 물리 PC 권장** |
| CI 아티팩트(windows-latest) | 빌드 산출물 확보. 단 **실행은 Windows에서** |
| 클라우드 Windows | Windows 365 Cloud PC / Azure VM / AWS EC2 |

**VM 소프트웨어 (macOS 호스트)**

| 맥 종류 | Windows | VM 앱 |
| --- | --- | --- |
| **Apple Silicon(M1~)** | Windows 11 **ARM64** | **Parallels Desktop**(가장 매끄러움·유료) · **UTM**(무료, QEMU) · **VMware Fusion**(개인 무료) |
| Intel Mac | Windows 11 **x64** | VMware Fusion(개인 무료) · Parallels · UTM |

- **WinUI 3는 ARM64 네이티브 빌드 가능**(csproj `RuntimeIdentifiers`에 `win-arm64` 포함) → Apple Silicon VM에서 **ARM64로 빌드·실행**이 빠르다. x64만 필요하면 Win11 ARM의 x64 에뮬레이션으로도 실행(느릴 수 있음).

**VM 절차**
1. VM 앱 설치 → Windows 11(ARM64/x64) 설치(MS 평가판 또는 정품).
2. 저장소 접근: VM 안에서 `git clone`, 또는 공유 폴더로 맥 작업트리 마운트.
3. 관리자 PowerShell에서 `scripts/bootstrap.ps1` 실행(choco→winget→수동 폴백).
4. `dotnet run --project app/Nexa.App` — **최초 1회 Windows App Runtime 1.6** 필요(bootstrap이 처리, [18 §6-2](18-build-and-test.md)).

**주의**
- VM은 **GPU 가속이 제한**될 수 있어 동작 확인엔 충분하나 **성능 측정은 물리 PC** 권장.
- ARM64 VM에서 **x64 빌드를 에뮬**로 돌리면 느림 → 가능하면 **ARM64 빌드**(`-r win-arm64`) 사용.
- 맥은 그대로 **코어(Rust) 개발·디버그**(§3, [18 §5-1](18-build-and-test.md))에 쓰고, **앱 실행 확인만 VM**에서 하는 분업이 효율적.

### 4-5. 도구별 패키지 ID & 수동 설치 (폴백)

> 부트스트랩 스크립트가 자동 처리하지만, **패키지 관리자로 설치되지 않으면 아래 URL에서 직접 설치**하세요.
> macOS=brew, Windows=choco→winget 순. (스크립트가 실패 시 동일 URL을 출력)

| 도구 | macOS (brew) | Windows (choco) | Windows (winget) | 수동 다운로드 |
| --- | --- | --- | --- | --- |
| Git | `git` | `git` | `Git.Git` | <https://git-scm.com/downloads> |
| Rust (rustup) | `rustup` | `rustup.install` | `Rustlang.Rustup` | <https://rustup.rs> (rustup-init) |
| .NET 8 SDK | `--cask dotnet-sdk` | `dotnet-8.0-sdk` | `Microsoft.DotNet.SDK.8` | <https://dotnet.microsoft.com/download/dotnet/8.0> |
| VS Build Tools 2022 | — (불필요) | `visualstudio2022buildtools` + 워크로드 | `Microsoft.VisualStudio.2022.BuildTools` (override) | <https://visualstudio.microsoft.com/downloads/> ("Build Tools") |
| VSCode | `--cask visual-studio-code` | `vscode` | `Microsoft.VisualStudioCode` | <https://code.visualstudio.com/download> |
| Windows Terminal | — | `microsoft-windows-terminal` | `Microsoft.WindowsTerminal` | <https://aka.ms/terminal> |
| (앱 실행용) Windows App Runtime 1.6 | — | — | `Microsoft.WindowsAppRuntime.1.6` (framework만 — 부족) | <https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe> **(권장: 전체 세트)** |

- **VS Build Tools 워크로드(수동 설치 시)**: "Desktop development with C++", ".NET desktop build tools", "Windows 11 SDK" 선택.
- **패키지 관리자 자체가 없을 때**: Homebrew <https://brew.sh> · Chocolatey <https://chocolatey.org/install> · winget(App Installer) <https://aka.ms/getwinget>.
- **VSCode 디버거 확장**(패키지 관리자 외): Windows `code --install-extension ms-vscode.cpptools`(cppvsdbg) · macOS/Linux `vadimcn.vscode-lldb`(CodeLLDB). 상세 [18](18-build-and-test.md) §5-1.
- **cargo-deny**(라이선스 게이트 로컬 실행): `cargo install cargo-deny`. CI는 `cargo-deny-action` 사용(별도 설치 불요).
- **Windows App Runtime**: winget `Microsoft.WindowsAppRuntime.1.6`은 **framework만** 설치 → unpackaged 앱 실행엔 Main/DDLM이 빠져 부족. 위 **인스톨러**(전체 세트)를 쓰거나 bootstrap.ps1에 맡긴다.

## 5. 저장소에 포함할 재현성 파일 (스캐폴딩 시)
- `rust-toolchain.toml` — Rust 버전 고정
- `global.json` — .NET SDK 버전 고정
- `scripts/bootstrap.ps1` — Windows 도구 설치
- `.vscode/{extensions,settings,launch}.json` — 권장 확장·디버그
- `.github/workflows/ci.yml` — windows-latest 빌드·테스트·아티팩트
- `README` 개발 섹션 — clone→bootstrap→build 3단계

## 6. CI 개요 (GitHub Actions)
```yaml
# .github/workflows/ci.yml (초안)
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest      # WinUI/패키징은 Windows 러너 필수
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - run: cargo test --workspace
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build app/Nexa.App -c Release
      # - 패키징: MSIX(makeappx/signtool) + 포터블(self-contained) 아티팩트 업로드
  core-macos:
    runs-on: macos-latest        # 코어 이식성 보증
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - run: cargo test -p nexa-core   # OS 비의존 부분
```

## 6-1. 맥에서 .NET/WinUI 빌드 가능한가? (검증 결과)

> 질문: "dotnet은 맥에서도 빌드 가능한 것 아닌가?" → **부분적으로 가능, 단 WinUI 3는 불가.** (2026-06-30 실측)

- **.NET 자체는 맥에서 동작**(설치됨: .NET 10 SDK). 크로스플랫폼 라이브러리/콘솔/ASP.NET은 맥에서 빌드·실행 OK.
- **맥에서 `dotnet build app/Nexa.App` 은 2단계로 실패한다** — **앱 빌드를 맥에서 시도하지 말 것**:
  - **1단계** (옵션 없이): 즉시 `NETSDK1100`
    ```
    error NETSDK1100: 이 운영 체제에서 Windows를 대상으로 하는 프로젝트를 빌드하려면
    EnableWindowsTargeting 속성을 true로 설정합니다.
    ```
    → Windows 타깃(`net8.0-windows`)이라 맥에선 기본 차단. **`-p:EnableWindowsTargeting=true` 로 우회하지 말 것**(아래 2단계로 갈 뿐).
  - **2단계** (`EnableWindowsTargeting=true` 우회 시): `XamlCompiler.exe` 실패
    ```
    XamlCompiler.exe: cannot execute binary file (exit 126)
    error MSB3073 ... Microsoft.UI.Xaml.Markup.Compiler
    ```
    WinUI의 **XAML 컴파일러가 Windows 전용 네이티브 실행파일**이라 macOS에서 실행 불가. (Wine 등 우회 비권장)
- **결론**: **WinUI 앱(`app/Nexa.App`)은 맥에서 빌드·실행 불가** → **Windows/VM/CI에서만**([§4-4](#4-4-macos에서-windows-앱-실행테스트--windows-vm-또는-pcci)).
  맥에서는 **코어(Rust)** 와 (후속) **크로스플랫폼 C# 라이브러리**만 빌드/테스트한다. (참고: 순수 `net8.0` Windows 라이브러리류는
  `EnableWindowsTargeting=true`로 맥 컴파일 가능하나, WinUI 앱은 위 2단계로 불가)

### 그래서 맥 개발을 최대화하는 방법 (권장)
1. **로직을 크로스플랫폼 .NET 라이브러리로 분리** — `Nexa.ViewModels`/`Nexa.AppCore`를 **`net8.0`(비 windows TFM)**
   클래스 라이브러리로 만들어 **맥에서 빌드·단위테스트**. XAML/WinUI 의존 코드만 Windows 전용 `Nexa.App`에 둔다.
   → MVVM 권장 구조와 일치, "얇은 UI" 원칙(맥 개발 surface 최대화)에 부합.
2. **Rust 코어**(`core/`)는 이미 맥 빌드·테스트 가능 — 비즈니스 로직 다수를 여기에 둔다.
3. **WinUI 셸 빌드/실행은 Windows**(PC/VM/CI). 맥에서 **앱 실행 테스트는 Windows VM** — 방법 [§4-4](#4-4-macos에서-windows-앱-실행테스트--windows-vm-또는-pcci).
4. (대안) Uno Platform/Avalonia는 맥 빌드 가능하나 **DR-1/DR-2(WinUI 확정)** 와 어긋나 채택 안 함.

> 정리: **"dotnet=맥 가능"은 맞지만 "WinUI 3=맥 불가"**. 맥에서는 Rust 코어 + 크로스플랫폼 C# 로직까지,
> WinUI XAML 셸만 Windows에서.

## 7. 요약
- **맥 우선 가능**: 코어는 맥에서 충분히 개발/테스트. UI/패키징만 Windows.
- **다른 Windows PC**: clone → `bootstrap.ps1` → 빌드/테스트(저장소만으로 재현).
- **CI**: Windows 러너가 신뢰 원천 — 로컬 Windows 없이도 빌드·산출물 확보.
