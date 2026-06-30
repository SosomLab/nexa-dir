#requires -Version 5.1
<#
.SYNOPSIS
  Nexa Dir Windows 개발 환경 부트스트랩.
.DESCRIPTION
  패키지 관리자 우선순위: Chocolatey(choco) → winget → (둘 다 실패 시) 수동 다운로드 안내.
  관리자 PowerShell에서 1회 실행. 상세: docs/11-dev-environment.md
.PARAMETER SkipVS
  VS Build Tools 설치를 건너뜀(이미 설치된 경우).
#>
[CmdletBinding()]
param([switch]$SkipVS)

$ErrorActionPreference = 'Continue'

function Write-Info($m) { Write-Host "▶ $m" -ForegroundColor Cyan }
function Write-OK($m)   { Write-Host "✓ $m" -ForegroundColor Green }
function Write-Warn($m) { Write-Host "! $m" -ForegroundColor Yellow }

$hasChoco  = [bool](Get-Command choco  -ErrorAction SilentlyContinue)
$hasWinget = [bool](Get-Command winget -ErrorAction SilentlyContinue)
Write-Info ("패키지 관리자 — choco: {0}, winget: {1}" -f $hasChoco, $hasWinget)
if (-not $hasChoco -and -not $hasWinget) {
  Write-Warn "choco/winget 모두 없습니다. Chocolatey 설치를 권장합니다:"
  Write-Host '  Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object Net.WebClient).DownloadString("https://community.chocolatey.org/install.ps1"))'
  Write-Host '  또는 winget(App Installer): https://aka.ms/getwinget'
}

# choco 우선 → winget 폴백 → 수동 안내
function Install-Pkg {
  param(
    [Parameter(Mandatory)][string]$Name,
    [string]$CheckCmd,           # 이미 설치 확인용 명령(있으면 건너뜀)
    [string]$Choco,              # choco 패키지 id
    [string]$ChocoParams,        # choco --params
    [string]$Winget,             # winget 패키지 id
    [string]$WingetOverride,     # winget --override
    [Parameter(Mandatory)][string]$ManualUrl
  )
  if ($CheckCmd -and (Get-Command $CheckCmd -ErrorAction SilentlyContinue)) {
    Write-OK "$Name (이미 설치됨)"; return
  }
  # 1) Chocolatey 우선
  if ($Choco -and $hasChoco) {
    Write-Info "choco install $Choco"
    $a = @('install', $Choco, '-y', '--no-progress')
    if ($ChocoParams) { $a += @('--params', $ChocoParams) }
    & choco @a
    if ($LASTEXITCODE -eq 0) { Write-OK "$Name (choco)"; return }
    Write-Warn "$Name choco 설치 실패 → winget 시도"
  }
  # 2) winget 폴백
  if ($Winget -and $hasWinget) {
    Write-Info "winget install $Winget"
    $a = @('install', '-e', '--id', $Winget, '--accept-package-agreements', '--accept-source-agreements')
    if ($WingetOverride) { $a += @('--override', $WingetOverride) }
    & winget @a
    if ($LASTEXITCODE -eq 0) { Write-OK "$Name (winget)"; return }
    Write-Warn "$Name winget 설치 실패"
  }
  # 3) 수동 다운로드 안내
  Write-Warn "'$Name' 을(를) 패키지 관리자로 설치하지 못했습니다 — 직접 설치하세요:"
  Write-Host "   다운로드: $ManualUrl"
}

# --- 도구 ---
Install-Pkg -Name 'Git'        -CheckCmd git     -Choco git              -Winget Git.Git                 -ManualUrl 'https://git-scm.com/downloads'
Install-Pkg -Name 'Rustup'     -CheckCmd rustup  -Choco 'rustup.install' -Winget Rustlang.Rustup         -ManualUrl 'https://rustup.rs  (rustup-init.exe)'

# .NET SDK — dotnet 런타임만 있고 SDK가 없는 PC가 있으므로 'dotnet --list-sdks'로 실제 SDK 유무를 확인한다.
# (CheckCmd=dotnet 만으로는 런타임만 있어도 '설치됨'으로 오판 → SDK 미설치 PC에서 빌드 불가)
$hasDotnetSdk = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
  $sdks = & dotnet --list-sdks 2>$null
  if ($sdks) { $hasDotnetSdk = $true }
}
if ($hasDotnetSdk) {
  Write-OK (".NET SDK (이미 설치됨: " + (($sdks | Select-Object -First 1) -replace ' \[.*$','') + " 등)")
} else {
  Install-Pkg -Name '.NET 8 SDK' -Choco 'dotnet-8.0-sdk' -Winget Microsoft.DotNet.SDK.8 -ManualUrl 'https://dotnet.microsoft.com/download/dotnet/8.0'
}

Install-Pkg -Name 'VSCode'     -CheckCmd code    -Choco vscode           -Winget Microsoft.VisualStudioCode -ManualUrl 'https://code.visualstudio.com/download'
Install-Pkg -Name 'Windows Terminal'             -Choco microsoft-windows-terminal -Winget Microsoft.WindowsTerminal -ManualUrl 'https://aka.ms/terminal'

# --- VS Build Tools (WinUI 네이티브 + Rust MSVC 링커 + 패키징; 풀 IDE 아님) ---
if (-not $SkipVS) {
  if ($hasChoco) {
    Write-Info "choco: VS Build Tools + 워크로드(VCTools, ManagedDesktop)"
    & choco install visualstudio2022buildtools -y --no-progress
    & choco install visualstudio2022-workload-vctools -y --no-progress
    & choco install visualstudio2022-workload-manageddesktopbuildtools -y --no-progress
    if ($LASTEXITCODE -ne 0) { Write-Warn "choco VS 설치 일부 실패 → winget/수동 확인" }
  }
  elseif ($hasWinget) {
    Install-Pkg -Name 'VS Build Tools 2022' -Winget Microsoft.VisualStudio.2022.BuildTools `
      -WingetOverride '--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Component.Windows11SDK.22621' `
      -ManualUrl 'https://visualstudio.microsoft.com/downloads/  ("Build Tools for Visual Studio 2022")'
  }
  else {
    Write-Warn "VS Build Tools — 직접 설치하세요:"
    Write-Host '   https://visualstudio.microsoft.com/downloads/  ("Build Tools for Visual Studio 2022")'
    Write-Host '   워크로드: Desktop development with C++, .NET desktop build tools, Windows 11 SDK'
  }
}

# --- Rust 안정 채널 + MSVC 타깃 ---
$rustupCmd = Get-Command rustup -ErrorAction SilentlyContinue
$rustup = if ($rustupCmd) { $rustupCmd.Source } else { "$env:USERPROFILE\.cargo\bin\rustup.exe" }
if (Test-Path $rustup) {
  & $rustup default stable
  & $rustup target add x86_64-pc-windows-msvc
} else {
  Write-Warn "rustup 미발견 — 설치 후 'rustup default stable; rustup target add x86_64-pc-windows-msvc' 실행"
}

Write-Host "`n✅ Windows 부트스트랩 완료. 새 터미널에서:" -ForegroundColor Green
Write-Host "   cargo test --manifest-path core/Cargo.toml"
Write-Host "   dotnet build app/Nexa.App -c Debug"
