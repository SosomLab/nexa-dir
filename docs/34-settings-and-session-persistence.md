# 34 · 설정·세션 영속화 — 저장 매커니즘 · 위치 · 데이터 범위

> 앱이 디스크에 남기는 상태를 **역할별로 분리**해 정리한다. 사용자 편집 대상(설정)과 앱이 관리하는
> 세션/창 상태를 **다른 파일**로 둔다. 현재 **`session.json`(탭 세션)·`settings.json`(사용자 설정, PREF-1) 구현 완료**,
> `state.json`(창 위치)은 설정 시스템 후속.
> 이 문서 = **영속 메커니즘·`session.json` 스키마·복원**의 단일 출처. 설정 스키마·페이지는 [40](40-preferences-system.md),
> 전체 외부 파일 물리 위치 맵은 [43](43-external-files-and-config.md), 언어팩은 [42](42-i18n-language-files.md).
> 관련: 창 위치 복원 [28](28-window-session-restore.md) · 코드 [SessionStore.cs](../app/Nexa.App/SessionStore.cs)·[SettingsStore.cs](../app/Nexa.App/SettingsStore.cs).

---

## 1. 파일 구성 (역할·위치·관리 주체)

| 파일 | 역할 | 위치 | 사용자 편집 | 상태 |
| --- | --- | --- | :---: | --- |
| **`session.json`** | **탭 세션**(활성 패널·열린 탭·경로·펼침·정렬·잠금/고정·**레이아웃·하단 패널**) | **`%LOCALAPPDATA%\NexaDir\`** | ✕(앱 관리) | **✅ 구현** |
| **`settings.json`** | 사용자 설정(테마·표시·메뉴·탭·언어·정렬) | `%APPDATA%\NexaDir\` | ○(설정 UI+직접) | **✅ 구현**(PREF-1, 스키마 [40 §4](40-preferences-system.md)) |
| `keybindings.json` | 단축키 재정의 | `%APPDATA%\NexaDir\` | ○ | 설계 |
| `state.json` | 세션 상태(팔레트 MRU/빈도·**창 위치/크기**) | `%APPDATA%\NexaDir\` | ✕(앱 관리) | 설계([28](28-window-session-restore.md)) |

- **Local vs Roaming 원칙**: 사용자 설정·키맵은 여러 PC로 **로밍(%APPDATA%)** 이 자연스럽다. 반면 **탭 세션은
  머신 고유 절대경로·열린 탭 목록**이라 다른 PC로 옮겨봐야 의미가 없어 **로컬(%LOCALAPPDATA%)** 에 둔다.
  (창 위치도 모니터 구성 의존이라 로컬 성격이나, 설계상 `state.json`은 %APPDATA% — β 구현 시 재검토.)
- **일반 설정과 별도 파일**: 요구대로 탭 세션은 `settings.json`과 **분리**된 `session.json`으로 저장 → 빈번한
  세션 쓰기가 사용자 설정 파일을 건드리지 않고, 손상 시 격리된다.

---

## 2. `session.json` — 데이터 범위(스키마)

저장 대상 = "다음 실행에서 마지막 작업 상태를 되살리는 데 필요한 것".

```jsonc
{
  "Version": 1,                 // 스키마 버전(후속 마이그레이션)
  "ActiveLeft": true,           // 활성(포커스) 패널: 좌=true / 우=false
  "Left":  { /* PanelSession */ },
  "Right": { /* PanelSession */ },
  "Bottom": { "Visible": true, "Height": 180, "Split": true, "LeftKind": 0, "RightKind": 0 }, // 하단 도킹 패널(BP-1)
  "Layout": { "ShowLauncher": true, "ShowRightPanel": true }   // 레이아웃 토글(머신 로컬, PREF-3 부분)
}
// PanelSession
{
  "ActiveTab": 0,               // 활성 탭 인덱스
  "Tabs": [                     // 열린 탭 목록(순서 유지)
    {
      "Path": "C:\\Users\\me\\Downloads",   // 탭의 현재 폴더
      "Expanded": [ "C:\\Users\\me\\Downloads\\sub" ],  // 인라인 펼침(열림)된 폴더 경로 집합
      "Sort": [ { "Key": 3, "Descending": true } ],     // 정렬 키(0=이름 1=확장자 2=크기 3=수정날짜 4=종류)
      "Locked": false,          // 탭 잠금(닫기 제외, TAB-MENU)
      "Pinned": false           // 탭 고정(핀 그룹 맨 앞)
    }
  ]
}
```
> **하단 패널·레이아웃**은 창/탭 배치라 **머신 로컬**(session.json에 동거). 표시 상세는 [43 §1](43-external-files-and-config.md).

| 저장 항목 | 원본 | 비고 |
| --- | --- | --- |
| 활성 패널 | `_activeLeft` | 좌/우 |
| 활성 탭 인덱스 | `PanelView.Tabs.IndexOf(Active)` | |
| 열린 탭 목록·순서 | `PanelView.Tabs` | 각 탭의 현재 경로 |
| 탭별 폴더 펼침 집합 | `PanelTab.Expanded` | 플래그십 인라인 트리 펼침 상태(F18) |
| 정렬 상태 | `PanelView.SortKeys` | **현재 아키텍처=패널 단위** → 각 탭에 동일 기록. 스키마는 탭 단위로 두어 per-tab 정렬(COL-2d) 후속 대비 |

**미저장(현재 범위 밖)**: 네비 뒤로/앞으로 히스토리(현재 경로만 저장), 선택/캐럿, 스크롤 위치, 컬럼 너비. → 후속 확장 여지.

---

## 3. 저장 매커니즘 (I/O 최소화 + 유휴 + 급종료 대비)

구현: [SessionStore.cs](../app/Nexa.App/SessionStore.cs). **요청(producer)과 수행(consumer)을 분리**해, 짧은 시간에
요청이 몰려도 **Tick당 최대 1회만** 저장한다.

1. **요청/수행 분리 + Tick 코얼레싱**
   - **요청** `MarkDirty()` — `dirty` 플래그만 set(초저비용·멱등). 상태 변경 지점마다 호출.
   - **수행** `OnTick()` — **단일 반복 타이머(1s)** 가 Tick마다 `dirty`를 **1회 소비**. N개의 요청 → **1회 쓰기**.
   - 소비 시점에 플래그를 먼저 해제 → 저장 도중 들어온 새 요청은 **다음 Tick**에 반영(유실 없음).
2. **유휴 실행**: Tick의 실제 저장을 `DispatcherQueuePriority.Low`로 큐잉 → UI가 한가할 때 수행(상호작용과 비경쟁).
3. **무변경 스킵**: 직렬화 결과의 SHA-256 해시가 직전 저장과 같으면 **디스크 쓰기 생략**(불필요 I/O 0).
4. **원자적 쓰기**: `session.json.tmp`에 쓰고 `File.Replace/Move`로 교체 → 쓰기 중 크래시에도 기존 파일 무손상.
5. **안전 주기(자가치유)**: 요청이 없어도 **60틱(≈60s)** 마다 1회 강제 캡처 → 훅 누락/장시간 세션 대비(무변경이면 쓰기 생략).
6. **종료 flush**: 창 `Closed` + `AppDomain.ProcessExit`에서 **즉시(동기)** flush → 정상 종료 시 최종 상태 확정.

- **급종료(작업관리자 강제 종료·전원 차단)**: 인프로세스 훅이 못 도는 경우라도, 디바운스(변경 후 ≤1틱)와 안전 주기(≤60s)로
  **손실 상한이 최근 1틱~1분**. 원자적 쓰기로 부분 파일 손상은 방지.

**저장 트리거(MarkDirty 호출 지점)**: 경로 이동(`ShowCurrent`) · 폴더 펼침/접힘(`ToggleExpandRow`) · 정렬 변경(`OnSortRequested`) ·
탭 전환(`SwitchToTab`) · 탭 닫기(`CloseTab`) · 활성 패널 변경(`SetActivePanel`).

---

## 4. 복원 (시작 시)

`MainWindow` 생성자에서 `RestoreOrDefaultSession()`:

1. `SessionStore.Load()`로 `session.json` 읽기(없거나 손상 시 `null` → 기본 시작: 좌=홈·우=문서).
2. 패널별로 **존재하는 폴더 경로만** 탭으로 복원(삭제/이동된 경로는 제외). 유효 탭이 하나도 없으면 그 패널은 기본 시작.
3. 패널 정렬(`SortKeys`)·펼침 집합(`Expanded`)을 로드 **전에** 세팅 → `OpenAndExpand`가 그대로 재적용.
4. **활성 탭만 즉시 로드**, 나머지 탭은 전환 시 지연 로드(시작 성능).
5. 활성 패널(`ActiveLeft`) 복원.

> 세션 저장 엔진(`_session`)은 **복원 이후 생성** → 복원 중 `MarkDirty` 소음 없음(훅은 `_session?.` 널가드).

---

## 5. 한계 / 후속

- **정렬 단위**: 현재 정렬은 패널 단위(`PanelView.SortKeys`)라 한 패널의 모든 탭이 같은 정렬. 스키마는 탭 단위로
  이미 저장 → **per-tab 정렬(COL-2d)** 구현 시 복원 로직만 탭별 적용으로 확장.
- **설정 시스템(β)과의 통합**: `SettingsStore`([26 §5-3](26-command-palette.md)) 도입 시 `settings.json`/`keybindings.json`/`state.json`
  로드·저장이 생긴다. `session.json`(탭)과 `state.json`(창/MRU)의 **경계·위치(Local/Roaming)** 를 그때 최종 확정
  (탭 세션을 `state.json`에 흡수할지, 별도 유지할지 결정 — 현재는 별도 `session.json` 유지).
- **확장 여지**: 네비 히스토리·선택/캐럿·스크롤·컬럼 너비 저장, 다중 창(멀티 윈도우) 세션.
