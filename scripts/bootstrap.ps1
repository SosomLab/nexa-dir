#requires -Version 5.1
<#
.SYNOPSIS
  Nexa Dir Windows 개발 환경 부트스트랩 (다른 PC 재현용).
.DESCRIPTION
  winget으로 Rust/.NET/VS Build Tools/VSCode 등을 설치하고 Rust MSVC 타깃을 추가한다.
  관리자 PowerShell에서 1회 실행. 상세: docs/11-dev-environment.md
#>
[CmdletBinding()]
param(
  [switch]$SkipVS  # VS Build Tools 설치를 건너뜀(이미 설치된 경우)
)

$ErrorActionPreference = 'Stop'

function Install-WingetPackage([string]$Id, [string]$Override = $null) {
  Write-Host "▶ winget install $Id" -ForegroundColor Cyan
  $args = @('install', '-e', '--id', $Id, '--accept-package-agreements', '--accept-source-agreements')
  if ($Override) { $args += @('--override', $Override) }
  winget @args
}

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
  throw 'winget 이 필요합니다. App Installer(Microsoft Store)를 설치하세요.'
}

Install-WingetPackage 'Git.Git'
Install-WingetPackage 'Rustlang.Rustup'
Install-WingetPackage 'Microsoft.DotNet.SDK.8'
Install-WingetPackage 'Microsoft.VisualStudioCode'
Install-WingetPackage 'Microsoft.WindowsTerminal'

if (-not $SkipVS) {
  # WinUI 네이티브 + Rust MSVC 링커 + 패키징 도구 (풀 VS IDE 아님)
  Install-WingetPackage 'Microsoft.VisualStudio.2022.BuildTools' `
    '--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Component.Windows11SDK.22621'
}

# Rust 안정 채널 + MSVC 타깃
& "$env:USERPROFILE\.cargo\bin\rustup.exe" default stable
& "$env:USERPROFILE\.cargo\bin\rustup.exe" target add x86_64-pc-windows-msvc

Write-Host "`n✅ 부트스트랩 완료. 새 터미널에서:" -ForegroundColor Green
Write-Host "   cargo test --manifest-path core/Cargo.toml"
Write-Host "   dotnet build app/Nexa.App -c Debug"
