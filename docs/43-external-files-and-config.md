# 43 · 외부 파일·환경값·언어팩 — 물리적 위치·기능·메커니즘 (레퍼런스)

> Nexa Dir가 디스크·환경에서 읽고 쓰는 **모든 외부 자원**의 단일 참조 문서.
> **어디에**(경로) · **무엇을**(기능) · **어떻게**(메커니즘·구조)를 정리한다. 설정 스키마 상세는 [40](40-preferences-system.md),
> 언어팩 상세는 [42](42-i18n-language-files.md), 세션 영속은 [SessionStore](../app/Nexa.App/SessionStore.cs)를 함께 참조.
> **원칙(NFR)**: 무간섭·오류격리 — 외부 자원 없음/손상 시 **격리하고 기본값으로 계속**(앱을 죽이지 않는다).

---

## 1. 한눈에 — 물리적 위치 맵

```
┌─ 설치 폴더  <exe 폴더>\  (배포 산출물, 앱과 함께 이동·업데이트로 갱신) ─────────────┐
│   Nexa.App.exe                                                                     │
│   nexa_interop.dll                네이티브 코어(Rust cdylib) — P/Invoke 대상        │
│   lang\en.lang, ko.lang           기본 제공 언어팩(i18n)                            │
│   lang\README.md                  번역 가이드                                       │
│   Assets\AppIcon\nexa-dir.ico     창/타이틀바 아이콘(AppWindow.SetIcon)             │
└────────────────────────────────────────────────────────────────────────────────────┘

┌─ 로밍  %APPDATA%\NexaDir\   ( = C:\Users\<사용자>\AppData\Roaming\NexaDir\ ) ────────┐
│   settings.json                   사용자 설정(테마·표시·메뉴·탭·언어·정렬) — PC 간 로밍│
│   lang\*.lang                     사용자 추가/오버라이드 언어팩(설치본보다 우선)       │
└────────────────────────────────────────────────────────────────────────────────────┘

┌─ 로컬  %LOCALAPPDATA%\NexaDir\  ( = ...\AppData\Local\NexaDir\ ) ───────────────────┐
│   session.json                    탭/패널/펼침/레이아웃/하단 패널 — 이 PC 전용        │
│   crash.log                       미처리 예외 로그(append)                           │
└────────────────────────────────────────────────────────────────────────────────────┘

┌─ 임베디드 (Nexa.App.dll 내부 — 파일 아님, 최후 안전망) ──────────────────────────────┐
│   Strings\en.json                 언어팩 폴더가 통째로 없을 때 UI 붕괴 방지용 en 폴백  │
│   (ApplicationIcon)               exe 임베드 아이콘(탐색기/작업표시줄 표시)            │
└────────────────────────────────────────────────────────────────────────────────────┘
```

**왜 로밍/로컬을 나눴나**: `settings.json`은 사람의 취향(테마·언어 등)이라 **여러 PC에서 공유되는 게 자연스러워** 로밍. `session.json`은 그 PC의 창/탭 배치·열린 폴더라 **머신 로컬**. 두 관심사를 **별도 파일·별도 위치**로 분리한다.

## 2. 파일별 상세

### 2-1. `settings.json` — 사용자 설정 (로밍)

| 항목 | 값 |
| --- | --- |
| 경로 | `%APPDATA%\NexaDir\settings.json` ([SettingsStore.DefaultPath](../app/Nexa.App/SettingsStore.cs)) |
| 엔진 | `SettingsStore` (영속 규율 §3 공용) |
| 로드 | `App.OnLaunched`(언어 결정용) + `MainWindow` 생성자에서 `Apply(Load())` — **다른 옵션 사용 전** |
| 저장 | 변경 지점에서 `MarkSettingsDirty()` → 디바운스 저장 + 종료 flush |
| 인메모리 대응 | `AppSettings`(static) ↔ `SettingsState`(DTO) 를 `Apply`/`Capture`로 왕복 |

**구조(섹션, 스키마 상세 [40 §4](40-preferences-system.md)):**
```jsonc
{
  "Version": 1,
  "Appearance": { "Mode": "Light" },                 // System|Light|Dark (테마, docs/39)
  "View":   { "ShowHiddenFiles": false, "ShowDotFiles": false, "ShowPathHeader": false,
              "AutoCloseTransferWindow": true, ... },  // 표시/동작 토글
  "Menu":   { "DisabledItems": [], "OrderOverrides": {}, "CustomSectionOnTop": false }, // 컨텍스트 메뉴 사용자화(docs/38 §7)
  "Tab":    { "DoubleClick": "..." },
  "General":{ "Culture": "" },                        // ""=시스템 / "ko" / "en" (i18n 선택)
  "Sort":   { ... }
}
```
> `Version`으로 후속 마이그레이션 대비. 미지정 키는 코드 기본값. 손상/부재 시 `Load`가 null → 전부 기본값으로 시작.

### 2-2. `session.json` — 탭/레이아웃 세션 (로컬)

| 항목 | 값 |
| --- | --- |
| 경로 | `%LOCALAPPDATA%\NexaDir\session.json` ([SessionStore.DefaultPath](../app/Nexa.App/SessionStore.cs)) |
| 엔진 | `SessionStore` (영속 규율 §3 공용) |
| 저장 대상 | 활성 패널(좌/우) · 패널별 열린 탭 목록 · 탭별(경로·펼침 폴더 집합·정렬·잠금/고정) · 레이아웃(런처/우 패널 표시) · 하단 패널(표시/높이/분리/콘텐츠) |

**구조:**
```jsonc
{
  "Version": 1,
  "ActiveLeft": true,
  "Left":  { "ActiveTab": 0, "Tabs": [ { "Path": "...", "Expanded": ["..."],
             "Sort": [{ "Key": 0, "Descending": false }], "Locked": false, "Pinned": false } ] },
  "Right": { ... },
  "Bottom":{ "Visible": true, "Height": 180, "Split": true, "LeftKind": 0, "RightKind": 0 },
  "Layout":{ "ShowLauncher": true, "ShowRightPanel": true }
}
```
> 정렬 스키마는 **탭 단위**로 두어 per-tab 정렬(후속) 확장 여지. `Locked`/`Pinned`는 기본 false로 과거 세션 파일과 호환.

### 2-3. `lang\*.lang` — 언어팩 (설치 + 사용자) → 상세 [42](42-i18n-language-files.md)

| 계층 | 경로 | 성격 |
| --- | --- | --- |
| 설치 | `<exe>\lang\*.lang` | 배포 기본(en, ko). 업데이트로 갱신 |
| 사용자 | `%APPDATA%\NexaDir\lang\*.lang` | 추가/오버라이드. 업데이트가 안 지움, **우선** |
| 안전망 | 임베디드 `Strings\en.json` | 위가 모두 없을 때만 |

- **엔진**: `LangCatalog`(탐색·병합) + `LangFormats.Active` 파서(현재 JSON, properties 전환 심).
- **로드 순서(키 단위 병합)**: 사용자 → 설치 → 임베디드 en → 키 문자열. 부팅 시 `App.OnLaunched` → `settings.General.Culture` → `Loc.Init` → `LangCatalog`.
- **파일 구조**: 평탄 JSON, `@`접두=메타(`@code`·`@name`·`@app`…), 나머지=문자열. UTF-8.
- **선택값의 출처는 `settings.json`의 `General.Culture`** (파일 내용과 별개). 언어 변경은 **재시작 반영**(마크업 확장 정적 조회).

### 2-4. `crash.log` — 진단 로그 (로컬)

| 항목 | 값 |
| --- | --- |
| 경로 | `%LOCALAPPDATA%\NexaDir\crash.log` ([App.LogCrash](../app/Nexa.App/App.xaml.cs)) |
| 기록 | 미처리 예외(UI 스레드·AppDomain·Task) — `[시각] 소스\n예외\n\n` **append** |
| 정책 | 로깅 실패도 무시(로깅이 크래시를 만들지 않게). 상한/로테이션은 후속 |

### 2-5. 네이티브 코어 · 아이콘 · 임베디드

- **`nexa_interop.dll`**: Rust 코어(cdylib). exe 옆에 배치, csproj가 `cargo`로 빌드([docs/18](18-build-and-test.md)). VFS/트리/ops 등 핫패스 P/Invoke 대상.
- **`Assets\AppIcon\nexa-dir.ico`**: 창/타이틀바 아이콘(`AppWindow.SetIcon`, [MainWindow](../app/Nexa.App/MainWindow.xaml.cs)). exe 임베드 아이콘은 csproj `ApplicationIcon`(탐색기/작업표시줄).
- **임베디드 `Strings\en.json`**: 언어팩 최후 안전망(§2-3).

## 3. 공용 영속 메커니즘 (`SettingsStore`·`SessionStore` 동일 규율)

두 저장 엔진은 **동일한 I/O 최소화 + 급종료 대비** 규율을 공유한다(무간섭 NFR):

1. **요청/수행 분리 + Tick 코얼레싱**: `MarkDirty()`는 dirty 플래그만 set(초저비용·멱등). 1초 주기 단일 타이머가 Tick마다 **최대 1회** 저장 → 요청이 몰려도 쓰기 1회.
2. **유휴 실행**: 실제 저장은 `DispatcherQueuePriority.Low`로 큐잉 → UI 한가할 때 수행(활성 상호작용과 경쟁 안 함).
3. **무변경 스킵**: 직렬화 결과의 **SHA-256 해시**가 직전과 같으면 디스크 쓰기 생략(불필요 I/O 0).
4. **원자적 쓰기**: `*.tmp`에 쓰고 `File.Replace`/`Move`로 교체 → 쓰기 중 크래시에도 기존 파일 무손상.
5. **안전 주기 자동저장**: 요청이 없어도 일정 주기(60 Tick)마다 1회 강제 캡처(훅 누락 자가치유, 무변경이면 쓰기 생략).
6. **종료 flush**: 창 Closed·`ProcessExit`에서 즉시(동기) 저장 → 정상 종료 시 최종 상태 확정.
7. **로드 격리**: 파일 없음/손상 시 예외를 삼키고 null 반환 → 기본값 시작(앱 방해 금지).

## 4. 환경변수·시스템 폴더 읽기

| 용도 | API / 변수 | 위치 |
| --- | --- | --- |
| 설정 루트(로밍) | `SpecialFolder.ApplicationData` | settings.json·사용자 lang |
| 세션·로그 루트(로컬) | `SpecialFolder.LocalApplicationData` | session.json·crash.log |
| 홈 폴백·초기 폴더 | `SpecialFolder.UserProfile` / `MyDocuments` | 좌/우 패널 초기 경로·경로 폴백 |
| 설치 폴더 | `AppContext.BaseDirectory` | lang\·아이콘·interop |
| 경로 입력 확장 | `%VAR%` → `Environment.GetEnvironmentVariable` | 경로 바 입력([PathInterpreter](../app/Nexa.ViewModels/PathInterpreter.cs)) |
| 터미널 셸 탐색 | `PATH` | 내장 터미널([ConPtySession](../app/Nexa.App/Terminal/ConPtySession.cs)) |
| 휴지통 | `CSIDL_BITBUCKET`(셸 특수 폴더) | 삭제/복원([RecycleBin](../app/Nexa.App/RecycleBin.cs)) |

> **비밀/자격 증명은 어떤 외부 파일에도 저장하지 않는다**(공개 대비, CLAUDE.md §6). 라이선스 인증(M7)은 앱에 **공개키만**, 서명 토큰은 별도 설계([17](17-licensing-activation.md)).

## 5. 부팅 시 읽기 순서 (요약)

```
App.OnLaunched
  ├─ SettingsStore.Load(settings.json)           → General.Culture
  ├─ Loc.Init(Culture) → LangCatalog.Discover/Load(lang\)  → Localizer.Current  (창 생성 전: 마크업 확장 정적 조회)
  └─ new MainWindow()
        ├─ SettingsStore.Apply(Load(settings.json))   → AppSettings.* (테마·표시·메뉴·탭·정렬)
        ├─ AppWindow.SetIcon(Assets\AppIcon\nexa-dir.ico)
        ├─ RestoreSession(session.json)               → 탭·펼침·레이아웃·하단 패널
        └─ SessionStore/SettingsStore 저장 엔진 가동(§3)
```

## 6. 후속·미구현 (참고)

- **창 위치/크기 복원**(다중 모니터 보정) — `state.json` 예정([28](28-window-session-restore.md)/Settings.cs 주석 참조). 현재 미저장.
- **단축키**(`keybindings.json`)·**컬럼**(per-tab)·**즐겨찾기** — 설정 시스템 후속(PREF-4/5/7, [40](40-preferences-system.md)).
- `crash.log` 로테이션/상한, 로그 레벨.
- 포터블 모드(설정을 앱 폴더 옆에 두는 옵션) — 배포 이원화 시 검토([12](12-packaging-portable.md)).
