# app/ — Nexa Dir UI 셸 (WinUI 3)

WinUI 3 (Windows App SDK) 기반 UI. **Windows 전용 빌드** (macOS/Linux 불가).

> ⚠️ **맥에서 빌드 금지**: `dotnet build app/Nexa.App` 실행 시 `NETSDK1100`(EnableWindowsTargeting) →
> 우회해도 `XamlCompiler.exe`(Windows 전용)로 실패. 맥에서는 **Windows VM**으로 실행 — 방법
> [docs/11 §4-4](../docs/11-dev-environment.md), 원인 [docs/11 §6-1](../docs/11-dev-environment.md).

## 빌드/실행 (Windows · 또는 Windows VM)

```powershell
# 사전: scripts/bootstrap.ps1 (또는 VS Build Tools + .NET 8 SDK + Windows App SDK)
dotnet build app/Nexa.App -c Debug
dotnet run  --project app/Nexa.App
```

## 구성

- `Nexa.App/` — 앱 진입점(App.xaml), 메인 윈도우(MainWindow.xaml). 비패키지(unpackaged) 구성.
- 후속 단위: 계층 경로 바 · 퀵 런처 · 듀얼 패널 · 인라인 트리/교차 선택(플래그십) · 인터롭(코어 cdylib 로드).

## 비고

- 현재는 **스캐폴딩**(빈 윈도우). macOS 개발 세션에서는 빌드되지 않으며 CI(windows-latest)/Windows PC에서 검증.
- 코어(Rust) 연동은 `nexa-interop` cdylib을 P/Invoke로 로드(다음 단위).
