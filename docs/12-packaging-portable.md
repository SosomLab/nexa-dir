# 12 · 패키징 & 포터블 배포 설계

> 결정(DR-3)은 **MSIX + Releases + winget**(설치형)이 1차. 추가로 **포터블 단일 exe** 배포도
> *가능하도록* 설계에서 미리 기술 고민을 해둔다. **우선순위 아님 — 배포 시점에 판단**.
> 핵심: "지금 구현"이 아니라 "**나중에 포터블이 가능하도록 지금 막지 않는다**".

---

## 1. 배포 형태 (3종 목표)

| 형태 | 용도 | 우선순위 |
| --- | --- | --- |
| **MSIX (설치형)** | 일반 사용자, winget/Store, 자동 업데이트, 셸 등록 | **1차 (DR-3)** — 서명 전략 선행(§6) |
| **setup.exe (클래식 설치기, Inno)** | 서명 전 임시/병행 설치 배포(사용자 단위 기본·언인스톨러·시작 메뉴) | **운영 중(PKG-5, §7)** |
| **포터블 폴더 (xcopy)** | 설치 없이 USB/공유폴더 실행, 자기완결 | **운영 중(PKG-1~3, §7)** |
| **포터블 단일 exe (self-extract)** | 파일 하나로 휴대 | 2차 (배포 시 판단) |

## 2. 우리 스택에서의 기술 과제 (Rust cdylib + WinUI 3 + .NET)

| 과제 | 내용 | 대응 |
| --- | --- | --- |
| WinUI 3 런타임 의존 | 기본은 Windows App SDK 런타임 설치 필요 | **Self-contained 배포**(WinAppSDK 런타임 번들) 사용 → 머신 설치 불필요 |
| .NET 런타임 의존 | 프레임워크 의존 시 .NET 설치 필요 | `--self-contained` + 런타임 포함 |
| 단일 파일 묶기 | exe 하나로 | `PublishSingleFile=true` + `IncludeNativeLibrariesForSelfExtract=true` (네이티브 dll 임베드→실행 시 temp 추출) |
| **Rust cdylib(.dll)** | C# 호스트가 로드하는 네이티브 라이브러리 | 단일 파일에 임베드·추출, 또는 포터블 폴더에 동봉 |
| WinUI 3 + 단일파일 제약 | 일부 WinAppSDK 네이티브/COM 활성화가 단일파일에서 까다로움(이력) | **조기 검증(패키징 스파이크)**, 안 되면 "포터블 폴더(zip)"로 폴백 |
| 셸 통합 등록 | 서드파티가 보는 컨텍스트 메뉴/썸네일 핸들러는 등록(레지스트리/MSIX) 필요 | **포터블은 등록형 셸 확장 제외** — 단, 우리 *앱 내부* 셸 메뉴 호스팅(IContextMenu 호출)은 정상 동작. 차이는 문서화 |

> 결론: **포터블 폴더(self-contained)** 는 무난, **단일 exe(self-extract)** 는 WinUI 제약 검증 후 결정.

## 3. 지금 설계에서 지켜야 할 것 (Portable-ready 원칙)

> 나중에 포터블을 *가능*하게 하려면 처음부터 아래를 지킨다.

1. **자기완결 가정** — 머신 전역 설치(런타임/레지스트리)에 **하드 의존하지 않음**.
   필수 기능은 self-contained 산출물만으로 동작.
2. **설정/상태 위치 전략 (포터블 모드)** —
   - exe 옆에 `portable.ini`(또는 `--portable`) 감지 시 **설정·캐시·인덱스를 exe 옆 `./data/`** 에 저장.
   - 없으면 표준 `%LOCALAPPDATA%\NexaDir\`(설치형). → 단일 코드 경로, 위치만 분기.
3. **레지스트리/관리자 권한 비의존(기본 기능)** — 권한 필요한 기능(셸 확장 등록, 파일연결)은
   **선택적·우아한 저하**. 포터블에서 비활성/대체.
4. **상대 경로·이식성** — 플러그인·캐시·설정 경로를 base-dir 기준 상대 처리.
5. **단일 인스턴스/정리** — 포터블 종료 시 temp 추출물 정리, 멀티 실행 안전.

## 4. 빌드 산출 (CI에서 동시 생성)

```
Actions (windows-latest)
├─ MSIX        : makeappx + signtool  → Releases + winget
├─ Portable-zip: dotnet publish -c Release --self-contained \
│                 -p:WindowsAppSDKSelfContained=true  → 폴더 zip
└─ Portable-exe: + PublishSingleFile=true \
                  -p:IncludeNativeLibrariesForSelfExtract=true (검증 후)
```

## 5. 로드맵 반영
- **지금(설계)**: §3 Portable-ready 원칙을 아키텍처 제약으로 채택(설정 위치 분기, 전역 의존 회피).
- **M0 스파이크**: "패키징 스파이크"에 **self-contained 포터블 폴더** 산출 검증 추가(단일exe는 후속).
- **배포 시점**: MSIX 우선, 포터블 폴더/단일exe는 수요·검증 결과로 on/off.

## 6. 미해결
- 단일 exe(self-extract)에서 WinUI 3 네이티브 활성화 완전동작 여부 → 패키징 스파이크로 확인.
- 코드 서명 인증서 전략(포터블 exe도 서명 권장) → 배포 전 결정(OD3/서명).
- **MSIX**: 패키징 프로젝트/매니페스트 + 서명(secrets) — 인증서 전략 결정 후 착수(PKG-4).
  서명 조사(07-11): Azure Artifact Signing=한국 개인 불가 → **Store 제출(서명 위임) 또는 OV 클라우드 서명**이 현실 경로.
- ~~포터블 게시 시작 중 `0xC0000374` 플레이키 크래시~~ → **원인 확정·해소(07-11, [BUG-010](BUGS.md))**: WinAppSDK 1.6 MRT Core
  `GetDefaultPriFile` 힙 손상(self-contained 한정, PageHeap으로 특정) — **WinAppSDK 1.8 업그레이드**로 해소.

## 7. 구현 현황 (2026-07-10 · `feat/packaging-portable` — 포터블 폴더 1차)

- **PKG-1 Portable-ready 경로 분기(§3-2)**: [`AppPaths`](../app/Nexa.App/AppPaths.cs) 신설 — exe 옆 `portable.ini` 또는 `--portable` 감지 시 설정·세션·사용자 언어팩·crash.log가 전부 `<exe>\data\`(미감지=표준 %APPDATA%·%LOCALAPPDATA%). 위치 맵 [43 §1](43-external-files-and-config.md).
- **PKG-2 산출 스크립트**: `scripts/make-portable.ps1` — self-contained 게시(.NET+**WinAppSDK 런타임 번들**, 머신 설치 불요) + 필수 산출 검증 + `portable.ini` 동봉 + `dist/NexaDir-<ver>-portable-<rid>.zip`(x64 ≈64MB, arm64는 `-Rid`).
- **PKG-3 CI**: `package` job(windows) — 태그 push·workflow_dispatch 시 zip 아티팩트 업로드.
- **자체 검증(이 PC)**: zip 추출 → 실행 → 정상 기동 → 종료 시 `data\settings.json`·`session.json` 생성, %APPDATA% 미오염 확인.
- **PKG-5 클래식 설치기 setup.exe(2026-07-11)**: `scripts/nexa-setup.iss`(Inno Setup 6 — CI windows 러너 기본 포함) + `scripts/make-setup.ps1`(동일 self-contained 게시, 마커 없음=설치형 표준 AppData 경로). **사용자 단위 기본**(`PrivilegesRequired=lowest`, 관리자 전체 설치는 대화상자 선택)·시작 메뉴/바탕화면(선택)·언인스톨러(사용자 데이터 보존)·`AppId` 고정(업그레이드 식별자). CI `package` job이 zip과 함께 빌드·릴리스 첨부. MSIX(PKG-4) 서명 전략 확정 전의 설치형 배포 채널 — 서명 없음(SmartScreen 경고 가능).
- **§2 과제 실측 2건**(빌드 트릭, csproj 게시 타겟으로 해소):
  1. **NETSDK1152**(중복 게시 파일) — 참조 WinUI 라이브러리들이 각자 WinAppSDK `MsixContent` 사본을 내놓음 → 내용 동일하므로 `ErrorOnDuplicatePublishOutputFiles=false`(게시 한정)로 무해화. 라이브러리 3종에 `RuntimeIdentifiers` 선언 필요(PRI252 대응).
  2. **라이브러리 XAML 미해석 크래시** — x64 Release 생성 코드가 루트 URI(`ms-appx:///NexaMenuBar.xaml`)를 쓰는데 `WindowsAppSDKSelfContained` 게시에선 pri 해석 실패 → 게시 루트에 **컴파일된 `.xbf` 배치**(`PublishLooseXamlAssets` 타겟). ⚠️ 초기의 loose 소스 `.xaml` 사본은 `x:Bind`가 죽어 **렌더링 결함**을 냈음(0.3.2, [BUG-010](BUGS.md) 결함 A). FD(런타임 설치 의존) 게시는 이 문제 없음.
  3. **(0.3.3) WinAppSDK 1.6→1.8** — self-contained 번들 1.6 MRT Core의 시작 중 힙 손상 크래시 해소([BUG-010](BUGS.md) 결함 B). 게시 스크립트는 **클린 산출**(publish 디렉터리 선삭제)로 잔재 오염 방지.
