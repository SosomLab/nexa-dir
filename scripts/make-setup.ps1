# make-setup.ps1 — 클래식 설치기(setup.exe) 산출 (docs/12 PKG-5)
#
# self-contained 게시(포터블과 동일 플래그, 단 portable.ini 미포함 = 설치형 표준 경로)를
# Inno Setup(nexa-setup.iss)으로 컴파일한다. 주 실행 환경 = CI(windows-latest, ISCC 기본 포함).
#   사용:  pwsh scripts/make-setup.ps1 [-OutDir dist]
#   산출:  dist/NexaDir-<version>-setup-win-x64.exe
param(
    [ValidateSet('win-x64')]   # Inno x64 전용 구성 — arm64는 후속(iss ArchitecturesAllowed 확장 필요)
    [string]$Rid = 'win-x64',
    [string]$OutDir = 'dist'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo 'app/Nexa.App/Nexa.App.csproj'
$platform = 'x64'

$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "csproj에서 <Version>을 찾지 못했습니다: $proj" }

Write-Host "== Nexa Dir $version setup ($Rid) =="

# 1) self-contained 게시 — make-portable.ps1과 동일 플래그, 별도 폴더(마커 없음 = 설치형)
$publishDir = Join-Path $repo "app/Nexa.App/bin/$platform/Release/publish-setup-$Rid"
# 이전 게시 잔재(구버전 loose 파일 등) 배제 — 항상 클린 산출
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $proj -c Release -r $Rid --self-contained `
    -p:Platform=$platform `
    -p:WindowsAppSDKSelfContained=true `
    -p:ErrorOnDuplicatePublishOutputFiles=false `
    -p:PublishDir=$publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패 ($LASTEXITCODE)" }

# 2) 필수 산출 검증 + 설치형 보장(portable.ini가 있으면 안 됨)
foreach ($required in @('Nexa.App.exe', 'nexa_interop.dll', 'lang/en.lang', 'lang/ko.lang')) {
    if (-not (Test-Path (Join-Path $publishDir $required))) { throw "게시 산출물 누락: $required" }
}
$marker = Join-Path $publishDir 'portable.ini'
if (Test-Path $marker) { Remove-Item $marker -Force }

# 3) ISCC 탐색 — PATH → 러너 표준(Program Files (x86)) → winget 사용자 설치
$iscc = @(
    (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup(ISCC.exe)을 찾지 못했습니다 — winget install JRSoftware.InnoSetup" }

# 4) 컴파일
$dist = Join-Path $repo $OutDir
New-Item -ItemType Directory -Force $dist | Out-Null
& $iscc "/DMyAppVersion=$version" "/DPublishDir=$publishDir" "/DOutputDir=$dist" `
    (Join-Path $PSScriptRoot 'nexa-setup.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC 컴파일 실패 ($LASTEXITCODE)" }

$exe = Join-Path $dist "NexaDir-$version-setup-win-x64.exe"
if (-not (Test-Path $exe)) { throw "산출물 없음: $exe" }
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "OK: $exe ($sizeMb MB)"
