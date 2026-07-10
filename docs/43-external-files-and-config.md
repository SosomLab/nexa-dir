# 43 · 외부 파일·환경값·언어팩 — 물리적 위치 맵 (인덱스)

> Nexa Dir가 디스크·환경에서 읽고 쓰는 **모든 외부 자원의 물리적 위치**를 한 장에 모은 **인덱스**.
> 각 자원의 *상세*(스키마·메커니즘)는 **소유 문서**가 관리하며, 여기서는 **위치 + 한 줄 기능 + 포인터**만 둔다(중복 회피).
>
> **소유 문서(single source of truth)**
> - 설정/세션 **영속 메커니즘·session.json 스키마·복원** → **[34](34-settings-and-session-persistence.md)**
> - **설정 의미·페이지·settings.json 스키마** → **[40](40-preferences-system.md)**
> - **언어팩(.lang) 포맷·병합·엔진** → **[42](42-i18n-language-files.md)**
> - 이 문서 = **물리적 위치 맵 + 소유 문서가 안 다루는 자원**(crash.log·네이티브 코어·아이콘·임베디드·환경변수·부팅 순서)
>
> **원칙(NFR)**: 무간섭·오류격리 — 외부 자원 없음/손상 시 **격리하고 기본값으로 계속**(앱을 죽이지 않는다).

---

## 1. 한눈에 — 물리적 위치 맵

```
┌─ 설치 폴더  <exe 폴더>\  (배포 산출물, 앱과 함께 이동·업데이트로 갱신) ─────────────┐
│   Nexa.App.exe                                                                     │
│   nexa_interop.dll                네이티브 코어(Rust cdylib) — P/Invoke 대상        │
│   lang\en.lang, ko.lang           기본 제공 언어팩(i18n)                → 상세 [42]  │
│   lang\README.md                  번역 가이드                                       │
│   Assets\AppIcon\nexa-dir.ico     창/타이틀바 아이콘(AppWindow.SetIcon)             │
└────────────────────────────────────────────────────────────────────────────────────┘

┌─ 로밍  %APPDATA%\NexaDir\   ( = C:\Users\<사용자>\AppData\Roaming\NexaDir\ ) ────────┐
│   settings.json                   사용자 설정(테마·표시·메뉴·탭·언어·정렬)  → 상세 [40]·[34]│
│   lang\*.lang                     사용자 추가/오버라이드 언어팩(설치본보다 우선) → 상세 [42]│
└────────────────────────────────────────────────────────────────────────────────────┘

┌─ 로컬  %LOCALAPPDATA%\NexaDir\  ( = ...\AppData\Local\NexaDir\ ) ───────────────────┐
│   session.json                    탭/패널/펼침/레이아웃/하단 패널        → 상세 [34]  │
│   crash.log                       미처리 예외 로그(append)              → §3         │
└────────────────────────────────────────────────────────────────────────────────────┘

┌─ 임베디드 (Nexa.App.dll 내부 — 파일 아님, 최후 안전망) ──────────────────────────────┐
│   Strings\en.json                 언어팩 폴더가 통째로 없을 때 UI 붕괴 방지용 en 폴백  │
│   (ApplicationIcon)               exe 임베드 아이콘(탐색기/작업표시줄 표시)            │
└────────────────────────────────────────────────────────────────────────────────────┘
```

**로밍 vs 로컬 원칙**: `settings.json`은 사람의 취향(테마·언어)이라 **여러 PC 로밍**이 자연스러움(%APPDATA%). `session.json`은 그 PC의 창/탭·열린 폴더 절대경로라 **머신 로컬**(%LOCALAPPDATA%). 상세·근거 → [34 §1](34-settings-and-session-persistence.md).

**포터블 모드(PKG-1, [12 §3](12-packaging-portable.md))**: exe 옆 `portable.ini` 존재 또는 `--portable` 인자 시 위 **로밍·로컬이 전부 `<exe>\data\`로 수렴**(settings.json·session.json·lang\·crash.log — USB/공유폴더 자기완결, 레지스트리/AppData 비사용). 분기 단일 원천 = [`AppPaths`](../app/Nexa.App/AppPaths.cs)(신규 영속 경로는 반드시 이를 경유).

## 2. 자원별 위치·기능 요약 (상세는 소유 문서)

| 자원 | 물리 위치 | 기능 | 엔진/코드 | 상세 |
| --- | --- | --- | --- | --- |
| `settings.json` | `%APPDATA%\NexaDir\` (로밍) | 사용자 설정(테마·표시·메뉴·탭·언어·정렬) | `SettingsStore` | 스키마 [40 §4](40-preferences-system.md)·메커니즘 [34 §3](34-settings-and-session-persistence.md) |
| `session.json` | `%LOCALAPPDATA%\NexaDir\` (로컬) | 탭·패널·펼침·정렬·잠금/고정·레이아웃·하단 패널 | `SessionStore` | [34 §2·§3·§4](34-settings-and-session-persistence.md) |
| `lang\*.lang` | 설치 `<exe>\lang\` + 사용자 `%APPDATA%\NexaDir\lang\` | UI 문자열(i18n) | `LangCatalog`·`LangFormats` | [42](42-i18n-language-files.md) |
| `crash.log` | `%LOCALAPPDATA%\NexaDir\` (로컬) | 미처리 예외 진단 로그 | `App.LogCrash` | §3 |
| `nexa_interop.dll` | `<exe>\` | 네이티브 코어(VFS·트리·ops) P/Invoke | csproj cargo 빌드 | [18](18-build-and-test.md) |
| `nexa-dir.ico` | `<exe>\Assets\AppIcon\` | 창/타이틀바 아이콘 | `AppWindow.SetIcon` | — |
| 임베디드 `en.json` | Nexa.App.dll 내부 | 언어팩 최후 안전망 | `Loc.EmbeddedTable` | [42 §2](42-i18n-language-files.md) |

> `settings.json`·`session.json`은 **동일 영속 규율**(요청/수행 분리·유휴 실행·무변경 SHA-256 스킵·원자적 쓰기·안전 주기·종료 flush)을 공유한다 — **상세 [34 §3](34-settings-and-session-persistence.md)**(코드 [SessionStore.cs](../app/Nexa.App/SessionStore.cs)). 언어 **선택값**의 출처는 `settings.json`의 `General.Culture`(언어 파일 *내용*과 별개, [42](42-i18n-language-files.md)).

## 3. `crash.log` — 진단 로그 (이 문서 소유)

| 항목 | 값 |
| --- | --- |
| 경로 | `%LOCALAPPDATA%\NexaDir\crash.log` ([App.LogCrash](../app/Nexa.App/App.xaml.cs)) |
| 기록 | 미처리 예외(UI 스레드·AppDomain·Task) — `[시각] 소스\n예외\n\n` **append** |
| 정책 | 로깅 실패도 무시(로깅이 크래시를 만들지 않게). 상한/로테이션은 후속 |

## 4. 환경변수·시스템 폴더 읽기 (이 문서 소유)

| 용도 | API / 변수 | 쓰임 |
| --- | --- | --- |
| 설정 루트(로밍) | `SpecialFolder.ApplicationData` | settings.json·사용자 lang |
| 세션·로그 루트(로컬) | `SpecialFolder.LocalApplicationData` | session.json·crash.log |
| 홈 폴백·초기 폴더 | `SpecialFolder.UserProfile` / `MyDocuments` | 좌/우 패널 초기 경로·경로 폴백 |
| 설치 폴더 | `AppContext.BaseDirectory` | lang\·아이콘·interop |
| 경로 입력 확장 | `%VAR%` → `Environment.GetEnvironmentVariable` | 경로 바 입력([PathInterpreter](../app/Nexa.ViewModels/PathInterpreter.cs)) |
| 터미널 셸 탐색 | `PATH` | 내장 터미널([ConPtySession](../app/Nexa.App/Terminal/ConPtySession.cs)) |
| 휴지통 | `CSIDL_BITBUCKET`(셸 특수 폴더) | 삭제/복원([RecycleBin](../app/Nexa.App/RecycleBin.cs)) |

> **비밀/자격 증명은 어떤 외부 파일에도 저장하지 않는다**(공개 대비, CLAUDE.md §6). 라이선스 인증(M7)은 앱에 **공개키만**([17](17-licensing-activation.md)).

## 5. 부팅 시 읽기 순서 (이 문서 소유)

```
App.OnLaunched
  ├─ SettingsStore.Load(settings.json)           → General.Culture
  ├─ Loc.Init(Culture) → LangCatalog.Discover/Load(lang\)  → Localizer.Current  (창 생성 전: 마크업 확장 정적 조회)
  └─ new MainWindow()
        ├─ SettingsStore.Apply(Load(settings.json))   → AppSettings.* (테마·표시·메뉴·탭·정렬)
        ├─ AppWindow.SetIcon(Assets\AppIcon\nexa-dir.ico)
        ├─ RestoreSession(session.json)               → 탭·펼침·레이아웃·하단 패널 (복원 상세 [34 §4])
        └─ SessionStore/SettingsStore 저장 엔진 가동 ([34 §3])
```

## 6. 후속·미구현 (참고)

- **창 위치/크기 복원**(다중 모니터 보정) — `state.json` 예정([28](28-window-session-restore.md)). 현재 미저장.
- **단축키**(`keybindings.json`)·**컬럼**(per-tab)·**즐겨찾기** — 설정 시스템 후속(PREF-4/5/7, [40](40-preferences-system.md)).
- `crash.log` 로테이션/상한, 로그 레벨.
- 포터블 모드(설정을 앱 폴더 옆에 두는 옵션) — 배포 이원화 시 검토([12](12-packaging-portable.md)).
