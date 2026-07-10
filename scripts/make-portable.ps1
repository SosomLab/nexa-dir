# make-portable.ps1 — 포터블 폴더(zip) 산출 (docs/12 §4, PKG-2)
#
# self-contained 게시(.NET + Windows App SDK 런타임 번들 — 머신 설치 불요)를 만들고
# portable.ini 마커(AppPaths 포터블 모드: 영속물=exe\data\)를 넣어 zip으로 묶는다.
#   사용:  pwsh scripts/make-portable.ps1 [-Rid win-x64] [-OutDir dist]
#   산출:  dist/NexaDir-<version>-portable-<rid>.zip
# 전제: cargo·dotnet(PATH) — scripts/bootstrap.ps1. MSIX(서명)는 후속(docs/12 §6).
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Rid = 'win-x64',
    [string]$OutDir = 'dist'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo 'app/Nexa.App/Nexa.App.csproj'
$platform = if ($Rid -eq 'win-arm64') { 'arm64' } else { 'x64' }

# 버전 = csproj <Version> (릴리스 태그와 동기)
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "csproj에서 <Version>을 찾지 못했습니다: $proj" }

Write-Host "== Nexa Dir $version portable ($Rid) =="

# 1) self-contained 게시 — WindowsAppSDKSelfContained=true로 WinAppSDK 런타임 dll 동봉.
#    ErrorOnDuplicatePublishOutputFiles=false: 참조 WinUI 라이브러리들이 각자 내놓는 동일 내용의
#    WinAppSDK MsixContent 사본이 NETSDK1152로 잡히는 알려진 충돌 무해화(게시 한정).
$publishDir = Join-Path $repo "app/Nexa.App/bin/$platform/Release/publish-$Rid"
dotnet publish $proj -c Release -r $Rid --self-contained `
    -p:Platform=$platform `
    -p:WindowsAppSDKSelfContained=true `
    -p:ErrorOnDuplicatePublishOutputFiles=false `
    -p:PublishDir=$publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패 ($LASTEXITCODE)" }

# 2) 필수 산출 검증 — 실행 파일 + 네이티브 코어 + 언어팩
foreach ($required in @('Nexa.App.exe', 'nexa_interop.dll', 'lang/en.lang', 'lang/ko.lang')) {
    if (-not (Test-Path (Join-Path $publishDir $required))) { throw "게시 산출물 누락: $required" }
}

# 3) 포터블 마커 — 존재만으로 포터블 모드(내용은 안내문, 파싱 안 함)
@"
; Nexa Dir portable marker — 이 파일이 exe 옆에 있으면 설정·세션·언어팩·로그가
; 이 폴더의 data\ 아래에 저장됩니다(레지스트리/AppData 비사용, docs/12 §3).
; 삭제하면 표준 설치형 위치(%APPDATA%·%LOCALAPPDATA%\NexaDir)를 사용합니다.
"@ | Set-Content -Path (Join-Path $publishDir 'portable.ini') -Encoding utf8

# 4) zip 묶기
$dist = Join-Path $repo $OutDir
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "NexaDir-$version-portable-$Rid.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "OK: $zip ($sizeMb MB)"
