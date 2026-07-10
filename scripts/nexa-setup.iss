; nexa-setup.iss — Nexa Dir 클래식 설치기(setup.exe) 정의 (docs/12 PKG-5)
; 호출은 make-setup.ps1이 담당: ISCC /DMyAppVersion=<ver> /DPublishDir=<self-contained 게시 폴더> /DOutputDir=<dist> nexa-setup.iss
; 서명 전 임시/병행 설치 배포(MSIX+서명=PKG-4 후속). 기본 사용자 단위 설치(관리자 불요) — 대화상자에서 전체 설치 선택 가능.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
  #error "PublishDir가 필요합니다 (self-contained 게시 폴더, portable.ini 미포함)"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define MyAppName "Nexa Dir"
#define MyAppPublisher "SosomLab"
#define MyAppURL "https://sosomlab.com"
#define MyAppExeName "Nexa.App.exe"

[Setup]
; AppId는 업그레이드 식별자 — 절대 변경 금지(변경 시 별도 앱으로 이중 설치됨)
AppId={{A3D1F9E7-6B2C-4B5E-9C41-0D8E2A7F5310}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\Nexa Dir
DisableProgramGroupPage=yes
; 사용자 단위 기본(서명 전 SmartScreen/UAC 마찰 최소) + 관리자 전체 설치 선택 허용
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=NexaDir-{#MyAppVersion}-setup-win-x64
SetupIconFile={#PublishDir}\Assets\AppIcon\nexa-dir.ico
LicenseFile=..\LICENSE.md
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; self-contained 게시 전체(런타임 번들) — portable.ini는 게시 단계에서 배제(설치형=표준 AppData 경로, docs/43)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; 제거 시 사용자 데이터(%APPDATA%·%LOCALAPPDATA%\NexaDir)는 보존(표준 관례) — 완전 제거는 사용자가 수동 삭제
