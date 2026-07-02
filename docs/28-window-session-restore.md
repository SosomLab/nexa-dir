# 28 · 창 위치/세션 복원 + 다중 모니터 보정 (설계)

> 목표: **마지막 실행의 창 위치·크기·상태를 기억**했다가 다음 실행에서 **그 자리로 복원**한다.
> 핵심 안전장치: 저장된 위치가 **현재 모니터 구성에 존재하지 않으면**(모니터 제거·해상도 변경·해제) 창이
> 화면 밖에 갇히지 않도록 **Primary 모니터의 LEFT/TOP 기준으로 옮겨 보여주는 오류 보정**을 둔다.
> 상태(구현): **설계**. 저장/복원 실체 구현은 설정 시스템(β, [26 §5](26-command-palette.md))·설정 화면 단위와 함께.

---

## 1. 저장 대상 (WindowPlacement)

세션 상태이므로 사용자 편집 대상이 아니다 → `state.json`([26 §5-1](26-command-palette.md), 앱이 관리).

```
WindowPlacement
  int   X, Y            // 복원 좌표(가상 데스크톱 좌표계, Primary 좌상단=0,0 · 음수 가능)
  int   Width, Height   // 복원 크기(px, DPI 미스케일 논리→물리 주의 §4)
  bool  Maximized       // 최대화 상태(복원 시 위치·크기 적용 후 최대화)
  // 최소화는 저장하지 않음 — 항상 "복원 시 보이는" 정상 상태 bounds를 저장
```

```jsonc
// state.json (발췌)
{
  "window": { "x": -1920, "y": 240, "width": 1400, "height": 900, "maximized": false }
}
```

- **저장 시점**: 창 크기/이동 종료(디바운스) + 종료 직전. 최대화 중이면 **복원(restore) bounds**를 저장(최대화 rect가 아니라 최대화 해제 시 크기).
- **최소화 상태에서 종료**: 최소화 bounds를 저장하지 말고 마지막 정상 bounds 유지.

## 2. 복원 + 오류 보정 (핵심 알고리즘)

시작 시 `state.json`의 `window`를 읽어 **현재 연결된 모니터 집합**에 대해 유효성을 검증하고, 무효면 보정한다.

```
restore(saved):
  monitors = enumerateWorkAreas()          // 각 모니터의 작업영역(작업표시줄 제외) 사각형
  rect = (saved.x, saved.y, saved.width, saved.height)

  if isVisibleEnough(rect, monitors):       // §2-1 가시성 판정 통과
      apply(rect)                           // 그대로 복원
  else:
      primary = primaryWorkArea()
      w = min(saved.width,  primary.width)   // 크기는 Primary 작업영역 안으로 클램프
      h = min(saved.height, primary.height)
      // ★사용자 요구: LEFT/TOP 기준으로 Primary 모니터에 옮김
      apply(primary.left, primary.top, w, h)

  if saved.maximized: maximize()            // 위치·크기 적용 후 최대화
```

### 2-1. "보이는가" 판정 `isVisibleEnough`

단순히 좌상단이 어느 모니터 안인지가 아니라 **드래그로 되찾을 수 있는가**로 판정한다.

- 각 모니터 작업영역과 `rect`의 **교집합 면적 합**을 구한다.
- 다음 중 하나라도 위반이면 **무효**로 간주 → Primary LEFT/TOP 보정:
  - 교집합 면적이 `rect` 면적의 **일정 비율 미만**(예: < 25%), 또는
  - **제목표시줄 띠**(상단 ~ +32px, 좌우 최소 100px)가 어떤 작업영역과도 **겹치지 않음**(잡을 곳이 없음).
- 예: 듀얼 모니터에서 오른쪽 모니터(가상좌표 x≥1920)에 있던 창을, 그 모니터를 떼고 재실행 → 교집합 0 → **Primary(0,0)로 이동**.

## 3. 트리거 상황 (왜 필요한가)

| 상황 | 결과(보정 없으면) | 보정 |
| --- | --- | --- |
| 외부 모니터 분리 | 창이 존재하지 않는 좌표에 → 화면 밖(접근 불가) | Primary LEFT/TOP |
| 해상도 축소(4K→1080p) | 창이 작업영역 밖으로 밀림 | 클램프 or 이동 |
| 모니터 배치 변경(오른쪽↔왼쪽) | 음수 좌표가 무효화 | 재검증→필요 시 이동 |
| DPI 배율 변경 | 크기 어긋남 | §4 재계산 |

## 4. WinUI 3 매핑 / 주의

- **위치·크기**: `AppWindow.MoveAndResize(RectInt32)` · 현재 값 `AppWindow.Position`/`Size`. 최대화/복원 = `OverlappedPresenter`(`Maximize()`/`Restore()`, `State`).
- **모니터 열거**: `Microsoft.UI.Windowing.DisplayArea` — `DisplayArea.FindAll()`, `DisplayArea.Primary`, `GetFromPoint`/`GetFromWindowId`. 작업영역 = `DisplayArea.WorkArea`(작업표시줄 제외), 전체 = `OuterBounds`.
- **DPI**: `DisplayArea` 좌표는 물리 픽셀. 저장/복원을 물리 픽셀로 통일하면 배율 변화에도 일관. per-monitor DPI 환경에서 이동 후 크기 재확인.
- **좌표계**: 가상 데스크톱(Primary 좌상단 0,0, 다른 모니터는 음수/양수 오프셋) → 저장값도 동일 좌표계.
- **저장 이벤트**: `AppWindow.Changed`(위치/크기 변화, 디바운스) + `Closed`. 최소화 판정은 `OverlappedPresenter.State`.

## 5. 단계적 구현 / 백로그

- **설계(현재)**: 본 문서 + `state.json.window` 스키마([26 §5-1](26-command-palette.md)).
- **구현(β)**: 설정 시스템(`SettingsStore`, [26 §5-3](26-command-palette.md))이 `state.json`을 로드/저장할 때 `window` 포함. 시작 시 `restore()` 적용, 종료/리사이즈 시 저장.
- **설정 화면 연동**: "시작 시 마지막 위치로 복원" on/off 옵션(기본 on)을 설정 UI에 노출(설정 화면 단위, [26 §8](26-command-palette.md)).
- **검증(맥 가능 부분)**: `isVisibleEnough`/보정 계산은 **UI 비종속 순수 로직** → `Nexa.Core`류로 분리해 맥에서 단위테스트(모니터 사각형 목록 + 저장 rect → 기대 결과). 실제 배치/DPI는 Windows 수동.
