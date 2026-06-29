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

## 3. macOS 환경 (코어 개발)

```bash
# Rust 툴체인
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
# 저장소
git clone git@github.com:SosomLab/nexa-dir.git && cd nexa-dir
# 코어 빌드·테스트 (Windows 전용부는 cfg로 제외되어 컴파일됨)
cargo test --workspace          # (core/ 스캐폴딩 후)
```
- VSCode 확장: **rust-analyzer**, CodeLLDB(디버그). 앱(WinUI)은 맥에서 실행 불가 — CI/Windows에서 확인.

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

### 4-2. 부트스트랩 스크립트 (다른 Windows PC도 동일 환경)
> 저장소에 `scripts/bootstrap.ps1`로 포함 예정. 관리자 PowerShell에서 1회 실행.

```powershell
# scripts/bootstrap.ps1 (초안)
winget install -e --id Git.Git
winget install -e --id Rustlang.Rustup
winget install -e --id Microsoft.DotNet.SDK.8
winget install -e --id Microsoft.VisualStudio.2022.BuildTools `
  --override "--quiet --add Microsoft.VisualStudio.Workload.VCTools `
              --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools `
              --add Microsoft.VisualStudio.Component.Windows11SDK.22621"
winget install -e --id Microsoft.VisualStudioCode
winget install -e --id Microsoft.WindowsTerminal
rustup default stable
rustup target add x86_64-pc-windows-msvc
```

### 4-3. 빌드·실행
```powershell
git clone https://github.com/SosomLab/nexa-dir.git ; cd nexa-dir
cargo build --release                      # 코어 cdylib
dotnet build app/Nexa.App -c Debug         # WinUI 앱 (스캐폴딩 후)
dotnet run  --project app/Nexa.App         # 실행
# 패키징은 §06/§12 참조 (MSIX / 포터블)
```

### 4-4. VM 옵션
- 보유 중인 **Windows 11 VM**(예: 경량 Win11 이미지) 사용 가능 — 동일 부트스트랩 적용.
- WinUI 3는 GPU 가속 사용 → VM은 가능하나 성능 측정은 물리 PC 권장.

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
- **Windows 타깃(net8.0-windows)** 도 `EnableWindowsTargeting=true` 옵션이면 맥에서 **복원/컴파일 시도**는 가능
  (참조 어셈블리 사용). WPF/WinForms 라이브러리류는 이렇게 맥/리눅스 CI 빌드가 됨.
- **그러나 WinUI 3(Windows App SDK)는 맥 빌드 불가** — 실측 에러:
  ```
  XamlCompiler.exe: cannot execute binary file (exit 126)
  error MSB3073 ... Microsoft.UI.Xaml.Markup.Compiler
  ```
  WinUI의 **XAML 컴파일러가 Windows 전용 네이티브 실행파일**이라 macOS에서 실행 불가. (Wine 등 우회는 비권장)

### 그래서 맥 개발을 최대화하는 방법 (권장)
1. **로직을 크로스플랫폼 .NET 라이브러리로 분리** — `Nexa.ViewModels`/`Nexa.AppCore`를 **`net8.0`(비 windows TFM)**
   클래스 라이브러리로 만들어 **맥에서 빌드·단위테스트**. XAML/WinUI 의존 코드만 Windows 전용 `Nexa.App`에 둔다.
   → MVVM 권장 구조와 일치, "얇은 UI" 원칙(맥 개발 surface 최대화)에 부합.
2. **Rust 코어**(`core/`)는 이미 맥 빌드·테스트 가능 — 비즈니스 로직 다수를 여기에 둔다.
3. **WinUI 셸 빌드/실행은 Windows**(PC/VM/CI). 맥 단독 시 CI 산출물로 확인.
4. (대안) Uno Platform/Avalonia는 맥 빌드 가능하나 **DR-1/DR-2(WinUI 확정)** 와 어긋나 채택 안 함.

> 정리: **"dotnet=맥 가능"은 맞지만 "WinUI 3=맥 불가"**. 맥에서는 Rust 코어 + 크로스플랫폼 C# 로직까지,
> WinUI XAML 셸만 Windows에서.

## 7. 요약
- **맥 우선 가능**: 코어는 맥에서 충분히 개발/테스트. UI/패키징만 Windows.
- **다른 Windows PC**: clone → `bootstrap.ps1` → 빌드/테스트(저장소만으로 재현).
- **CI**: Windows 러너가 신뢰 원천 — 로컬 Windows 없이도 빌드·산출물 확보.
