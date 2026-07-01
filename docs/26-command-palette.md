# 26 · 커맨드 팔레트 + 명령 레지스트리 (설계)

> 목표: **모든 기능을 키보드로**(Ctrl+Shift+P) 검색·실행. VSCode/Sublime식 팔레트.
> 핵심: 메뉴·단축키·컨텍스트 메뉴·런처·플러그인·팔레트가 **하나의 명령 레지스트리(SSOT)** 를 공유하고,
> 단축키·사용자 명령·팔레트 상태를 **JSON 설정**으로 재정의·영속화한다.
> 요구: **FR-I2**(단축키 재정의·명령 팔레트 P1)·**FR-J4**(단일 액션 레지스트리)·FR-K5·FR-L1([05](05-requirements.md)). 마일스톤 **M1(레지스트리 토대)→P1(팔레트 UI)**. 구현 대상 등록.

---

## 1. 왜 "팔레트"가 아니라 "레지스트리"가 먼저인가

기존 문서 전반이 **단일 액션 레지스트리**를 전제한다([02](02-roadmap.md)·[03](03-features.md)·[09](09-plugin-architecture.md)·[10](10-decision-record.md), FR-J4). 팔레트는 그 레지스트리 위의 **검색 UI 한 겹**일 뿐이다. 따라서:

- **명령(Command)** 을 1급 데이터로 정의 → **메뉴·툴바·단축키·컨텍스트 메뉴·런처·팔레트·플러그인**이 모두 같은 명령을 참조.
- 새 기능은 "명령 하나 등록"으로 끝 → 자동으로 팔레트·단축키·메뉴에 노출 가능(중복 배선 없음).
- 앞서 만든 [Settings.cs](../app/Nexa.App/Settings.cs)의 `AppSettings`는 명령이 **읽고/바꾸는 상태**가 되고, 팔레트 토글 명령이 그 값을 뒤집는다(예: "폴더 우선 정렬" ↔ `Sort.FoldersFirst`).

## 2. 명령 모델

```
Command
  string    Id            // 안정 식별자, 점 표기: "view.sort.foldersFirst"
  string    Title         // 표시명: "폴더 우선 정렬 켜기/끄기"
  string    Category      // 그룹: "보기" · "파일" · "이동" · "선택"
  string?   DefaultKey    // 기본 단축키(사용자 재정의 가능): "ctrl+shift+p"
  string?   When          // 활성 컨텍스트: "panelFocused" · "hasSelection"
  string?   Icon          // Segoe MDL2 글리프(메뉴/툴바 공유)
  Func      CanExecute    // 회색 처리 여부
  Action    Execute       // 실제 동작(코어 ops 호출·설정 변경 등)
  bool      Checkable     // 토글 명령이면 체크 상태 표시
  Func<bool> IsChecked
CommandRegistry
  Register(Command) / Get(id) / All() / Search(query)
  event  Changed          // 플러그인 기여·설정 변경 시 재빌드
```

- **명령 소스**: ① 내장(앱), ② 플러그인 기여(FR-L1 — 인터롭 이벤트 스트림 → 레지스트리, [09 §UI기여](09-plugin-architecture.md)), ③ 사용자 정의(외부 실행, §5-3).
- **컨텍스트(When)**: 활성 패널·선택 유무·뷰 모드 등 키/값을 `ContextService`가 보유 → 팔레트/단축키가 `When`으로 필터.

## 3. 팔레트 UX

- **호출**: `Ctrl+Shift+P`(명령). 오버레이 입력창 + 결과 리스트(가상화). ESC/바깥 클릭 닫기.
- **접두 모드**(prefix)로 대상 전환:
  | 접두 | 모드 | 내용 |
  | --- | --- | --- |
  | `>` (기본) | 명령 | 레지스트리 명령 검색·실행 |
  | `/` 또는 `~` | 경로 이동 | 경로/최근 폴더/즐겨찾기로 점프(FR-A) |
  | `@` | 선택/항목 | 현재 목록 항목으로 점프(대용량 목록 필터) |
  | `?` | 도움말 | 사용 가능한 접두·명령 안내 |
- **퍼지 매칭**: 부분/이니셜(예: `pff`→ "**P**olders **F**irst… "), 한글 자모 매칭 고려. 매치 하이라이트.
- **랭킹**: 최근 사용(MRU) + 빈도 가중 → 상단. 상태로 **영속화**(§5-2).
- **행 표시**: `카테고리 · 제목 ······ 단축키`(현재 유효 키), 토글 명령은 체크 표시.
- **인자 입력**(chained): 인자가 필요한 명령은 선택 후 **후속 입력 단계**로 전환(예: "새 폴더" → 이름 입력).

## 4. 단축키 해석 (Keymap)

- **기본 키**는 명령의 `DefaultKey`. **사용자 재정의**는 `keybindings.json`(§5-1)이 우선.
- `KeymapService`: (키 조합 + When 컨텍스트) → 명령 해석. 충돌 시 마지막(사용자) 우선, 팔레트에 경고.
- 팔레트·메뉴는 항상 **현재 유효 키**를 표시(재정의 즉시 반영).

## 5. JSON 설정 연동 ★(핵심)

기존 `AppSettings`([Settings.cs](../app/Nexa.App/Settings.cs))를 **파일 기반 설정 시스템**으로 확장하고, 명령/단축키/팔레트를 그 위에 얹는다. 위치: `%APPDATA%/NexaDir/`. 직렬화 = `System.Text.Json`. **변경 시 저장 · 실행 시 로드**(주석 허용 JSONC).

### 5-1. 파일 구성 (역할 분리)

| 파일 | 역할 | 사용자 편집 |
| --- | --- | --- |
| `settings.json` | 앱 설정(정렬·팔레트·명령 활성) | ○(설정 UI + 직접) |
| `keybindings.json` | 단축키 재정의(FR-I2) | ○ |
| `commands.user.json` | 사용자 정의 명령(외부 실행, FR-K3) | ○ |
| `state.json` | 세션 상태(팔레트 MRU/빈도·최근 경로) | ✕(앱이 관리) |

```jsonc
// settings.json — AppSettings 확장(§Settings.cs가 이 스키마로 로드/저장)
{
  "sort": { "foldersFirst": true },        // ← 기존 SortOptions(F15). 팔레트 토글이 이 값을 변경
  "commandPalette": {
    "fuzzy": true,
    "recentCount": 8,
    "showCategories": true,
    "hangulJamoMatch": true
  },
  "commands": {                            // 명령별 오버라이드
    "view.sort.foldersFirst": { "enabled": true }
  }
}

// keybindings.json — 명령 id ↔ 키(+선택 When). 기본 키를 덮어씀
[
  { "key": "ctrl+shift+p", "command": "workbench.commandPalette.show" },
  { "key": "f2",           "command": "file.rename",     "when": "hasSelection" },
  { "key": "ctrl+shift+f", "command": "view.sort.foldersFirst" }   // 재정의 예
]

// commands.user.json — 외부 프로그램을 명령/팔레트/런처 공용 액션으로(FR-K3/K5)
[
  { "id": "user.openInVSCode", "title": "VS Code로 열기", "category": "도구",
    "exec": "code", "args": ["%path%"], "icon": "" }
]
```

### 5-2. 양방향 연동 (읽기 + 쓰기)

- **읽기**: 실행 시 4개 파일 로드 → 레지스트리 구성(기본 명령 + 사용자 명령), 키맵 해석, 팔레트 랭킹 복원.
- **쓰기**: 팔레트/설정 UI에서의 변경이 **즉시 해당 파일에 저장**되어 다음 실행에 유지.
  - 예) 팔레트에서 "폴더 우선 정렬 끄기" 실행 → `AppSettings.Sort.FoldersFirst=false` → `settings.json` 저장 → 열린 목록 재정렬([19 F15](19-implemented-features.md) 후속과 연결).
  - 예) 팔레트 명령을 자주 쓰면 MRU/빈도가 `state.json`에 누적 → 다음에 상단.
- **감시(선택)**: 파일 외부 편집을 watcher로 감지해 핫리로드(β 이후).

### 5-3. `AppSettings` 확장 경로 (기존 코드와의 연속)

현재 `AppSettings`는 인메모리 싱글턴(코드 기본값). 아래로 단계 확장:

```
AppSettings (기존)                    →  SettingsStore (확장)
  static SortOptions Sort                 Load()  : 4개 JSON 로드 → 각 옵션 채움
                                          Save(section) : 변경 섹션만 직렬화
                                          event Changed : 구독자(목록 재정렬 등) 통지
                                          CommandRegistry / Keymap 이 참조
```
- `SortOptions.FoldersFirst`는 **스키마의 첫 입주자** → 설정 시스템이 실제로 무언가를 저장/복원함을 F15로 이미 검증.
- 마이그레이션: `settings.json`에 `schemaVersion` 필드 → 향후 필드 추가/개명 대응.

### 5-4. 현재 하드코딩 단축키 → 재정의 대상 (★ 할 일)

> 지금 코드에 **하드코딩**된 키들을, 레지스트리 명령의 `DefaultKey`로 옮기고 `keybindings.json`으로 **사용자 재정의 가능**하게 이관한다(FR-I2). 신규 단축키는 처음부터 명령으로 등록한다.

| 현재 키 | 동작 | 명령 id(예정) | 구현 |
| --- | --- | --- | --- |
| `↑` / `↓` | 선택 이동 | `nav.move.up`/`down` | F14/F16 |
| `Shift+↑/↓` | 범위 선택 확장 | `select.range.up`/`down` | F17 |
| `Ctrl+↑/↓` | 캐럿만 이동(비연속) | `caret.move.up`/`down` | F17 |
| `Space` / `Ctrl+Space` | 단일 선택 / 토글 | `select.single`/`select.toggle` | F17 |
| `→` / `←` | 폴더 펼침 / 접힘 | `tree.expand`/`tree.collapse` | F17 |
| `←`(접힘 상태) | 부모로 이동 | `nav.parent.row` | F17-1 |
| `Alt+↓` | 활성화(폴더 진입 / 파일 실행) | `item.activate` | F19 |
| `Alt+↑` / `Alt+←` / `Alt+→` | 위로 / 뒤로 / 앞으로 | `nav.up`/`nav.back`/`nav.forward` | F21 |
| (탭) 영역 더블클릭 | 새 탭 | `tab.new` | F20 |
| (예정) `Ctrl+T/W/Shift+T`·`Ctrl+Tab` | 탭 열기/닫기/복원/전환 | `tab.*` | FR-B |
| `Alt`·`Alt+문자` | 메뉴 토글/접근 | (메뉴 시스템) | F12 |

- 이관 시 **동작 코드는 명령의 `Execute`로 이동**, 키 입력 핸들러(`OnGridKeyDown`)는 **Keymap 조회 → 명령 실행**으로 축소.
- 기본 키맵 세트(위 표)를 `defaults`로 두고, `keybindings.json`이 덮어씀(§5-1).

## 6. 우리 스택 매핑

| 구성 | 위치 / 기술 |
| --- | --- |
| 명령 레지스트리 · 키맵 · 컨텍스트 | **C# 앱 계층**(도메인 명령 카탈로그 앎). 순수 로직은 `net8.0`로 분리해 **맥 단위테스트** 가능([11 §6-1](11-dev-environment.md)) |
| 팔레트 UI(오버레이·퍼지·가상화) | Windows(WinUI). 결과 리스트는 `NexaFileGrid`류 가상화 재사용 검토 |
| 설정 로드/저장 | `System.Text.Json`(JSONC), `%APPDATA%/NexaDir/` |
| 명령 실행 본체 | 대부분 코어 호출(ops/nav/검색) 또는 설정 변경. 코어 API는 인터롭 경유 |
| 플러그인 기여 명령 | 인터롭 이벤트 스트림 → 레지스트리 병합(FR-L1, [09](09-plugin-architecture.md)) |

> **맥 개발 이점**: 레지스트리·키맵·퍼지매처·설정 직렬화는 **UI 비종속 순수 로직** → `Nexa.Core.Commands`(net8.0)로 빼면 맥에서 풍부히 단위테스트(키 파싱·When 필터·MRU·JSON 왕복). 팔레트 UI만 Windows.

## 7. 단계적 구현

- **α (M1 토대)**: `CommandRegistry` + 기본 명령 등록(네비게이션·정렬 토글·선택·표시) + **팔레트 UI(Ctrl+Shift+P)** + 퍼지 검색 + 현재 단축키 표시. 상태는 인메모리.
  - 이때 **메뉴(NexaMenuBar)·툴바를 레지스트리 구동으로 전환**(트랙 D "실동작 연결"과 통합) → 배선 중복 제거.
- **β (P1, 설정 시스템과 동반)**: `settings.json`/`keybindings.json`/`state.json` 로드·저장(§5), 단축키 재정의(FR-I2),
  **현재 하드코딩 단축키(§5-4)를 명령 레지스트리로 이관** → `keybindings.json`으로 변경 가능하게, MRU/빈도 영속화, 팔레트 환경설정.
- **γ (후속)**: 접두 모드(경로/항목 점프), `commands.user.json` 외부 실행(FR-K3/K5 런처와 공용), 플러그인 기여 명령(FR-L1), 파일 핫리로드, 한글 자모 퍼지.

## 8. 로드맵 / 백로그

- FR-I2·FR-J4를 본 설계로 구체화. [02](02-roadmap.md) M1 "단일 액션 레지스트리" + P1 "명령 팔레트 고도화"에 링크.
- **★ 할 일(사용자 요청)**: 현재 하드코딩된 키보드 단축키(§5-4: 이동/선택/펼침/`Alt+↓` 등)를 **명령 레지스트리 + `keybindings.json`으로 이관해 사용자가 변경 가능**하게. 신규 단축키도 명령으로 등록.
- [04](04-trends-todo.md) "명령 팔레트(Ctrl+P)" → 본 설계로 승격. 구현 순서는 [19](19-implemented-features.md) 트랙 D와 통합(메뉴/툴바 실동작을 레지스트리로 전환).
- 미해결(착수 전 확정): 기본 키맵 세트(참조 앱 대비표)·`state.json` vs `settings.json` 경계·JSONC 파서(주석 허용) 채택 여부.
