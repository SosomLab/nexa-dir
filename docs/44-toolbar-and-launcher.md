# 44 · 도구 모음(내장) · 퀵 런처(외부 프로그램) 설계

> 두 종류의 "빠른 실행 도구"를 구분·통합한다.
> - **도구 모음(Toolbar)** = **개발자 제공 내장 기능**(앱 안 동작을 바로 실행). 예: 현재 폴더 터미널·파일 찾기·이름 변경.
> - **퀵 런처 바(Quick Launcher)** = **사용자 등록 외부 프로그램**(exe 실행). 예: VS Code로 활성 탭 폴더 열기.
> 둘 다 **16×16 아이콘** 버튼이며, 향후 **단축키 배정**(PREF-5 keybindings)으로 빠른 실행에 연결한다.
> 관련: 퀵 런처 요구 PREF-6([40 §3](40-preferences-system.md)) · 단축키 [26 §5](26-command-palette.md) · 검색 M3 [24](24-search.md).
> 상태: **슬라이스 1 구현**(2026-07-08, `feat/toolbar-launcher`) — 버튼·실행·아이콘·시드.
> **슬라이스 2 구현**(2026-07-10, `feat/toolbar-groups`, §5) — 그룹화·파일 표시 토글·순서 설정·터미널 위치 이동.
> CRUD(런처)·단축키 배정은 후속.

---

## 1. 구분 (누가 정의하나)

| | 도구 모음 | 퀵 런처 바 |
| --- | --- | --- |
| 정의 주체 | **개발자**(내장 액션 고정 세트) | **사용자**(등록/수정/삭제) |
| 실행 대상 | 앱 내부 기능(코드 액션) | 외부 프로그램(exe + 인자) |
| 아이콘 | Segoe MDL2 글리프(16px) | **exe에서 추출한 아이콘**(16px 썸네일) |
| 위치 | 툴바 행(Row 1) | 퀵 런처 행(Row 2, 표시 토글) |
| 영속 | 불필요(코드 고정) | `settings.json` `Launcher`(후속) |

## 2. 슬라이스 1 — 구현 범위

### 2-1. 도구 모음(내장) 3종
| 도구 | 글리프 | 동작 | 단축키 |
| --- | --- | --- | --- |
| 현재 폴더 터미널 | `E756` | **외부 터미널** 열기 — wt → pwsh → powershell → cmd 폴백(현재 폴더) | **Ctrl+Shift+T** |
| 파일 찾기 | `E721` | 전면 검색은 **M3 예정** → 현재는 안내 스텁 | (후속) |
| 이름 바꾸기 | `E8AC` | 활성 패널 캐럿 항목 인라인 이름 변경(F2와 동일) | **F2** |

### 2-2. 퀵 런처(외부) 시드 1종
- **Visual Studio Code — 활성 탭 폴더 열기**: `Code.exe "%path%"`(`%path%`=활성 탭 폴더). **exe 아이콘 16px** 표시.
- exe 경로 추정([`ToolLauncher.ResolveVsCode`](../app/Nexa.App/ToolLauncher.cs)): 사용자(`%LOCALAPPDATA%\Programs\...`)·시스템(`%ProgramFiles%[(x86)]\Microsoft VS Code\Code.exe`). 미설치면 시드 제외.

## 3. 구현 매핑

| 구성 | 파일 |
| --- | --- |
| 프로세스 실행 헬퍼(터미널·외부 프로그램·VS Code 탐지) | [`ToolLauncher`](../app/Nexa.App/ToolLauncher.cs) (실패 격리·폴백) |
| 버튼 구성·아이콘 로드·액션 | `MainWindow.InitToolbars`/`AddToolButton`/`AddLauncherButton`/`LoadExeIconAsync` |
| 내장 액션 | `OpenTerminalHere`·`FindFilesStub`·`RenameCaret`(F2 공용) |
| 외부 실행 | `LaunchTool`(`%path%`=활성 탭 폴더) |
| UI 호스트 | `MainWindow.xaml` `ToolbarToolsHost`(Row 1)·`LauncherHost`(Row 2) |
| exe 아이콘 | `StorageFile.GetThumbnailAsync(SingleItem, 16)` → BitmapImage(실패 시 라벨 폴백) |

- **인자 템플릿**: `%path%`=활성 탭 폴더(치환). 후속에 `%selection%`(선택 파일들)·`%file%` 추가.
- **오류 격리**: 실행 실패(미설치·권한)는 false → 상태바 안내(앱 무중단).

## 4. 후속 슬라이스

1. **CRUD + 영속**(PREF-6): 설정 창 "런처" 페이지 — 도구 추가/수정/삭제/순서, `settings.json` `Launcher.Items`(라벨·exe·인자·아이콘 오버라이드). ~~도구 모음도 표시/숨김·순서 사용자화~~ → 순서는 §5에서 구현(표시/숨김은 잔여).
2. **단축키 배정**(PREF-5): 액션 레지스트리 + `keybindings.json` — 각 도구(내장·런처)에 사용자 지정 제스처. 지금은 터미널(Ctrl+Shift+T)·이름변경(F2) 하드코딩.
3. **인자 토큰 확장**: `%selection%`·`%file%`·`%parent%` 등(일괄 리네임 UDF 변수와 정렬, [25](25-bulk-rename.md)).
4. **아이콘 커스텀**: 런처 항목별 아이콘 지정(exe 외 커스텀 ico/png).
5. **파일 찾기 실동작**: M3 검색([24](24-search.md)) 연결(현재 스텁 대체).

## 5. 슬라이스 2 — 그룹화·파일 표시 토글·순서 설정·터미널 위치 이동 (2026-07-10, PR#10)

- **그룹 레지스트리**: `MainWindow.ToolbarGroupDef/ItemDef` — 그룹(도구 `tools`·파일 표시 `view`) 기반
  `BuildToolbar()`가 (재)구성, 그룹 경계는 세로 구분선. 설정 변경 시 라이브 재구성(OnPreferencesChanged).
- **파일 표시 토글 2종**(`view` 그룹): **숨김 파일 표시**(켜짐 `E7B3` RedEye / 꺼짐 `ED1A` Hide 아이콘 교체) ·
  **도트파일(.) 표시**(`E712` ⋯, 꺼짐=흐림). 표시(S) 메뉴·빈영역 메뉴와 같은 설정 공유 —
  `SetShowHidden/SetShowDotFiles` 단일 경로 + `SyncViewMenuChecks→SyncToolbarToggles` 동기.
- **순서 설정**: 설정 "도구 모음" 카테고리 — 그룹/항목 ▲▼ 이동, `ToolbarOptions(GroupOrder/ItemOrder)`
  → `settings.json` `Toolbar` 섹션. 규칙=나열 id 우선·미기재 id는 기본 순서 뒤(새 항목 안전).
- **터미널 위치 이동**: ① 도구 모음 — 터미널 버튼에 밀착한 22×22 정사각(Attached, `E72A`)
  ② 하단 도크 정보란 — 터미널 토글과 한 몸 스타일(라운딩 분할·간격 0·터미널 활성 시 초록 강조).
  동작 = 해당 패널 활성 탭 폴더로 내장 터미널 `cd`(cmd는 `/d`), 터미널 탭 자동 전환,
  세션 미시작이면 시작 직후 전송(`TerminalView` pending 입력). 탭 전환 시 도크 폴더 갱신 버그 수정(`RefreshBottomDocks`).
