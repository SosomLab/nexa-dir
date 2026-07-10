# 40 · 설정(Preferences) 시스템 — 통합 설정 창 · 페이지 · 스키마 · 영속

> 사용자 요청(2026-07-08) 기능 다수를 담는 **단일 설정 시스템**의 설계. 흩어진 인메모리 옵션과 개별 기능
> (테마·레이아웃·컬럼·단축키·퀵 런처·즐겨찾기·언어)을 **하나의 설정 창 + `settings.json`** 으로 통합한다.
> 관련: 영속 매커니즘 [34](34-settings-and-session-persistence.md) · 액션/명령 레지스트리 [26](26-command-palette.md) ·
> 테마 [39](39-theme-system.md) · 컬럼 [23](23-column-system.md) · 컨텍스트 메뉴 [38 §7](38-adr-0005-shell-context-menu.md).
> 상태: **부분 구현** — S1 인프라+창(PREF-1) ✅ · **글꼴 6종 슬롯 + VS Code식 창 재구성(검색·트리) ✅(2026-07-10, §8)**.
> 남은 페이지(컬럼·단축키·런처·즐겨찾기·테마팩)는 슬라이스로(§5).

---

## 1. 목표 / 원칙

- **하나의 설정 창**(`Ctrl+,` / 구성 메뉴 → 설정)에 **페이지**로 분류. 각 페이지는 독립 편집기.
- **단일 백킹**: `settings.json`(로밍, §4) — 섹션별 스키마. 단축키만 `keybindings.json` 분리(편집 관례·[34]).
- **라이브 적용**: 가능한 항목은 즉시 반영(테마·레이아웃 토글). 재시작 필요 항목(언어)은 명시.
- **기존 자산 재사용**: 이미 만든 인메모리 옵션(`AppSettings.Theme/Menu/View/Tab/Sort`)이 각 페이지의 백킹.
  지금은 코드 기본값이 유일 원천 → **이 시스템이 로드/저장을 붙인다**(재시작 소실 해소).
- **확장 가능**: 페이지·항목을 레지스트리로(플러그인 M6가 페이지 기여 가능하게 여지).

## 2. 아키텍처

```
SettingsStore (영속)  ──load/save──►  AppSettings (인메모리 그룹들)
      │  settings.json(로밍) · keybindings.json                 ▲
      │                                                         │ 바인딩/적용
      ▼                                                         │
PreferencesWindow ──[IPrefPage 목록]──► 좌 내비 + 우 편집기 ───┘  라이브 적용 이벤트
```

- **`SettingsStore`**: `session.json`과 동일 엔진 규율(요청/수행 분리·원자적 쓰기·무변경 스킵, [34 §3]) 재사용.
  단, 세션과 달리 **사용자 편집**이라 저장 트리거는 설정 창의 커밋 시점 + 앱 종료.
- **`IPrefPage`**(제목·아이콘·`FrameworkElement Build()`·`Apply()`): 페이지 목록을 레지스트리로 관리.
- **적용 통지**: 값 변경 시 `SettingsChanged(section)` 이벤트 → 해당 서브시스템 갱신(예: Theme→`ApplyTheme`,
  Menu→다음 메뉴 열림 시 반영, Layout→토글 즉시).

## 3. 페이지 카탈로그 (요청 기능 매핑)

| 페이지 | 내용 | 백킹(스키마) | 현재 | 요청 |
|---|---|---|---|---|
| **모양(Appearance)** | 테마 모드(시스템/라이트/다크)·**테마팩**(토큰 색 오버라이드)·**폰트**(UI/모노)·**크기(밀도)** | `ThemeOptions`([39 §5]) | 모드 ✅(메뉴) | 테마 설정 |
| **레이아웃(Layout)** | 퀵 런처/우 패널(듀얼)/하단 패널/하단 분리 표시·경로·항목 수 헤더·전송 창 자동 닫기·시작 시 복원 | `ViewOptions`(+표시 토글) | 토글 ✅(메뉴) | 레이아웃 설정 |
| **컬럼(Columns)** | 표시 컬럼 선택·순서·너비·정렬 기본값·per-tab 정렬 옵션(COL-4/COL-D3/4) | `ColumnOptions`(신규) | 정렬 ✅·조정 모달 ☐ | 컬럼 설정 |
| **단축키(Keybindings)** | 액션별 키 재정의·충돌 검사·기본 복원 | `keybindings.json`([26 §5]) | ☐ | 단축키 지정 |
| **런처(Launcher)** | 등록 도구(라벨·경로·인자 템플릿 `%path%`/`%selection%`·아이콘)·순서·추가/삭제 | `LauncherOptions`(신규) | placeholder | 퀵 런처 바 설정 |
| **즐겨찾기(Favorites)** | 즐겨찾기·핀 폴더 목록 관리(추가/삭제/순서·구분)·사이드바 노출 | `FavoritesOptions`(신규) | ☐(사이드바 B-7) | 즐겨찾기 관리 |
| **메뉴(Context Menu)** | 커스텀 항목 표시/순서·섹션 위치([38 §7]) | `MenuOptions` ✅ | 스키마 ✅·UI ☐ | (연계) |
| **언어(Language)** | UI 언어 선택(재시작/부분 라이브) | `GeneralOptions.Culture` | ☐ | i18n |
| **일반(General)** | 시작 동작·업데이트·기본 폴더·확인 프롬프트 등 | `GeneralOptions`(신규) | ☐ | — |

- **미리보기**: 각 페이지 하단에 현재 설정으로 그린 미니 프리뷰(메뉴/컬럼/테마) — 후속.

## 4. `settings.json` 스키마 (로밍, %APPDATA%\NexaDir)

```jsonc
{
  "Version": 1,
  "Appearance": { "Mode": "Light", "Pack": "", "UiFont": "Segoe UI", "MonoFont": "Consolas", "Density": "Compact" },
  "Layout":     { "ShowLauncher": true, "ShowRightPanel": true, "ShowBottom": true, "BottomSplit": true,
                  "ShowPathHeader": false, "AutoCloseTransferWindow": true },
  "Columns":    { "Visible": ["name","ext","modified","kind","size"], "Widths": {}, "DefaultSort": [{"Key":0,"Desc":false}] },
  "Launcher":   { "Items": [ {"Label":"pwsh","Path":"pwsh.exe","Args":"-NoExit -Command \"cd '%path%'\"","Icon":""} ] },
  "Favorites":  { "Items": [ {"Label":"Downloads","Path":"C:\\Users\\me\\Downloads"} ] },
  "Menu":       { "DisabledItems": [], "OrderOverrides": {}, "CustomSectionOnTop": false },
  "General":    { "Culture": "ko-KR", "ConfirmPermanentDelete": true }
}
```

- **마이그레이션**: `Version` + 섹션별 누락=기본값. 키 rename 시 마이그레이션 표. `keybindings.json`은 별도 파일.
- 인메모리 그룹(`AppSettings.*`)과 1:1 — `SettingsStore.Load`가 채우고, 창 커밋이 저장.

## 5. 구현 슬라이스

1. **S1 인프라**: `SettingsStore`(load/save·마이그레이션) + 기존 그룹(Theme/Menu/View/Tab) 영속 배선 + `Ctrl+,` 빈 설정 창.
2. **S2 모양·레이아웃**: 두 페이지(이미 스키마 있음) — 재시작 소실 즉시 해소.
3. **S3 컬럼(COL-4)·런처**: 컬럼 조정 UI + 퀵 런처 실기능(등록/실행).
4. **S4 즐겨찾기·단축키·언어**: 사이드바(B-7)·`keybindings.json`·i18n(D-2) 인프라와 각각 결합.

## 6. 별도 기능 (설정 창 밖 — 참조만)

요청 중 설정이 아니라 **독립 기능**인 것 — 각자 문서/마일스톤에 등록(§TODO):

- **압축 파일 지원(내장 zip)**: zip 폴더처럼 탐색(VFS Provider) + 압축/해제(전송 엔진 통합). **별도 ADR** 후보
  (VFS Provider 계약 C-2와 연계 — 아카이브를 가상 폴더로). 로드맵 M-후속(§02 반영).
- **일괄 이름 변경**: 규칙 스택+실시간 미리보기 — **이미 설계 [25](25-bulk-rename.md)**·로드맵 M1 §파일작업. 진입점만 추가.
- **파일 찾기(Everything식)**: **M3** 검색([24](24-search-everything.md)) — MFT/USN 인덱스. 설정 창엔 "검색" 페이지(범위·제외)만 후속.

## 7. 관계 정리

- **설정 창이 담는 것** = 테마·레이아웃·컬럼·단축키·런처·즐겨찾기·메뉴·언어·일반 (본 문서 페이지).
- **설정 창이 여는 것** = 별도 기능(압축/일괄 이름변경/검색)의 옵션 일부만(범위·기본값), 기능 자체는 독립.
- 저장은 전부 `settings.json`(+`keybindings.json`) 단일 경로 → 재시작 유지·로밍.

## 8. 구현 현황 (2026-07-10 — `feat/font-settings`)

- **설정 창 재구성(VS Code식)**: 상단 **설정 검색**(AutoSuggestBox) + 좌측 **카테고리 TreeView**
  (모양[테마·글꼴] / 파일 목록 / 탭 / 파일 작업 / 컨텍스트 메뉴 / 언어) + 우측 편집기.
  모든 항목은 **설정 레지스트리**(`PreferencesWindow.Entry` 목록 — 카테고리·라벨 키·빌더)로 등록되어
  카테고리 렌더와 검색(라벨 부분 일치, 카테고리 경로 캡션)이 같은 원천을 쓴다. §2의 `IPrefPage` 대신
  이 레지스트리가 그 역할(페이지=카테고리, 항목=Entry).
- **글꼴 5종 슬롯(구 "폰트" 항목)**: 기본(메뉴·**탭·경로(브레드크럼)**·설명·하단 정보/미리보기)/**콘솔**(터미널,
  쉼표 폴백 목록)/상태표시줄/파일 목록(+컬럼 헤더)/파일 헤더(두껍게·기울임 꾸미기만)+**폴더 굵게** 토글.
  경로 글꼴 슬롯은 도입 후 기본 글꼴로 통합·제거(2026-07-10 사용자 결정). 탭 높이=기본 글꼴 크기 연동.
  `FontOptions`↔`FontSettings`(settings.json `Fonts` 섹션) + `MainWindow.ApplyFonts` 라이브 적용.
  UI = 편집 가능 콤보(설치 글꼴 목록 = GDI `InstalledFonts` + 직접 입력) — 엔터/포커스 이탈/목록 선택 시
  검증(미설치 글꼴 경고·미적용). 터미널은 셀 폭 실측(`TerminalView.ApplyFont`) 후 격자 리사이즈.
- **영속 스키마 실제(v1)**: `Appearance{Mode}` · `Fonts{…6종}` · `View{…}` · `Sort{FoldersFirst}`(신규) ·
  `Menu{…}` · `Tab{DoubleClick}` · `General{Culture}` — §4의 예시 스키마 중 Columns/Launcher/Favorites는 미구현.
- **전 영속 설정 UI 수록**: 숨김·점 파일·경로 헤더·폴더 우선·상위 이동 정렬 위치·타입어헤드(범위/리셋)·
  탭 더블클릭·전송 창 자동 닫기·OS 클립보드(예정 표기)·드래그 dwell 2종·커스텀 메뉴 항목/위치·언어.
  dwell은 변경 시 타이머 즉시 재반영.
