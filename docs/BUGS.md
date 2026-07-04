# BUGS — 알려진 이슈 / 버그 백로그

> 미해결 버그를 추적한다. 해결 시 커밋 해시와 함께 "해결" 표기 후 하단 이동/삭제.
> 형식: 증상 → 재현 → 근본 원인(확인 방법) → 시도한 해결책과 결과 → 후보 해결책.

---

## BUG-001 · 빈 폴더에서 나오면 대상 폴더 목록이 빈 화면 (✅ 해결)

- **심각도**: 높음(기능 저해 — 자주 밟는 경로)
- **상태**: **✅ 해결**(2026-07-03, 사용자 실기 QA 확인). 관찰된 blank는 모두 **아래 "시도한 해결책"의 실험 코드(UpdateLayout/`ItemsSource` null 토글/RefreshRealization)가 켜진 빌드에서만** 나타났고, 그 실험을 되돌린 뒤(2차 라운드 main 병합 `1d9d312`) **재현되지 않음**을 반복 QA로 확인 → **실험 코드 자체가 ItemsRepeater realization을 망가뜨린 원인**이었다(순수 P1 로드 경로엔 문제 없었음). 이후 A-2(범위 diff 통지)·A-4(요소 수명 기반 아이콘 로드)로 목록 실체화 경로가 더 안정화됨. 재발 시 재오픈.
- **연관**: **P1 비동기 로드([4c6f74c])가 노출**시킨 회귀. 동기 로드(P1 이전)에선 입력 이벤트 직후 레이아웃이 돌아 실체화가 자연 복구돼 드러나지 않았음. 스크롤 수정([cfecd64])과는 별개(그건 정상 동작).

### 증상
헤더에는 항목 수가 **정확히** 표시되는데(코어·개수 정상), 목록 영역이 **빈 화면**으로 남는다. 한 번 이 상태가 되면 이후 다른 폴더로 이동해도 계속 빈 화면으로 **고착**된다(예: 59개 폴더로 가도 안 보임).

### 재현 (100%)
1. `C:\Users\kiros33` → `Videos` 진입
2. `Videos\화면 녹화` 진입 — **완전히 빈(0개) 폴더**
3. `Alt+↑`(상위) 또는 뒤로/앞으로로 `Videos` 복귀 → **목록 빈 화면**
   - `C:\Users\kiros33\Torrent`(자식=빈 `Download`뿐)에서도 동일: `Download` 진입 후 상위 이동 시 blank.
4. 대조군(정상): 홈 → `Videos`로 **아래로** 내려가면 잘 보임(빈 폴더를 거치지 않음).
- 공통 조건: **직전에 빈(0개) 폴더를 거쳤을 때만** 발생.

### 근본 원인 (진단 로그로 확인)
`ItemsRepeater`는 `EffectiveViewport`(뷰포트) 변화에 따라 요소를 실체화한다. 빈(0개) 소스가 되면 리페이터의 뷰포트/실체화 상태가 무너지고, 뷰포트가 **동일한**(오프셋 0, 같은 뷰포트 높이) 폴더로 복귀하면 `EffectiveViewportChanged`가 안 떠서 **재실체화가 트리거되지 않는다**. 소스(ItemsSource)나 강제 레이아웃과 무관하게 **ScrollViewer↔ItemsRepeater 뷰포트 체인**에 고착된다.

진단 계측(임시 `DebugRealization`) 로그 핵심 대비:

```
정상(홈→Videos):     viewCount=3  elem0=True   extH=283   ← 요소 실체화됨
버그(빈폴더→상위):    viewCount=3  elem0=False  extH=283   ← 개수 3인데 요소 0개 실체화, 이후 고착
```

`viewCount`(ItemsSourceView 개수)는 3으로 정상 → 코어/컬렉션/개수는 문제 없음. `elem0=False`(index 0 요소 미실체화)가 핵심. `extH`(ExtentHeight)가 빈 폴더 값(283)에 **고정**된 채 새 항목 수를 반영하지 못함 = 리페이터 measure/실체화 정지.

### 시도한 해결책과 결과 (모두 실패 — 직접 디버깅용 기록)
| # | 시도 | 위치 | 결과 |
|---|---|---|---|
| 1 | `onLoaded`을 `DispatcherQueuePriority.Low`로 지연 | MainWindow.LoadDirectory | 스크롤엔 부분 효과, **blank 무효** |
| 2 | 채택 후 `grid.UpdateLayout()` 강제 | MainWindow.LoadDirectory | **무효**(elem0=False 그대로) |
| 3 | `ItemsSource = null; = tab.Items`(null 토글로 ItemsSourceView 재생성) | MainWindow.LoadDirectory | **무효** → 스테일 뷰/개수가 원인이 **아님**을 입증(재바인딩해도 실체화 안 됨) |
| 4 | `Repeater.Visibility = Collapsed → UpdateLayout → Visible → UpdateLayout` | NexaFileGrid.RefreshRealization | **무효**(elem0=False) |
| 5 | `BodyScroll.Content = null → UpdateLayout → = content → UpdateLayout`(뷰포트 체인 재수립) | NexaFileGrid.RefreshRealization | **blank 무효** + **부작용: 시작 시 2–5초 행(진행 아이콘) + 간헐 즉시 종료(크래시)**. 매 로드마다(시작 시 양 패널 포함) 호출돼 비용·재진입 문제. → 되돌림 |

> 위 시도는 모두 **되돌렸다**(마지막 정상 상태 = 스크롤 수정 [cfecd64]). 현재 코드에는 이 실험/계측이 없다.

### detach/reattach(시도 #5) 비용에 대한 메모
`ScrollViewer.Content = null` 후 재설정은 **리페이터의 실체화된 시각 하위트리 전체를 파괴·재생성**(가상화 요소 재사용 이점 상실) + **동기 `UpdateLayout` 2회**(강제 measure/arrange)를 유발한다. 이를 **매 내비게이션마다 무조건** 수행하면 낭비가 크고, 큰 폴더에선 재부착 시 뷰포트를 처음부터 동기 재실체화한다. 관찰된 2–5초 행·간헐 크래시는 단순 비용을 넘어 **시작 시점(시각 트리 미완성)·비동기 연속에서의 재진입/레이아웃 스래싱**이 겹친 결과로 보인다. 결론: **부적합**. 만약 유사 기법을 쓴다면 (a) **빈 폴더에서 나오는 특정 전이에서만** 조건부로, (b) 시작/재진입 타이밍을 피해 적용해야 한다.

### 후보 해결책 (우선순위 순, 미검증)
1. **본문 `ItemsRepeater`를 새 인스턴스로 교체** — 고착 상태 없는 새 요소. `x:Name` 대신 코드 생성 컨테이너로 보관하고 **빈-폴더 전이에서만** 교체(비용 한정). `ItemTemplate`/`Layout`은 코드로 재설정. NexaFileGrid 소폭 리팩토링 필요. **가장 확실**.
2. **`ItemsRepeater`+`ScrollViewer` → `ListView`/`ItemsView`로 대체** — 내장 가상화가 빈→비어있지 않음 전이에 견고. 단, 현재 커스텀 컬럼/템플릿·성능 특성 재검토 필요(ADR-0002 영향).
3. **뷰포트를 확실히 흔드는 방법 탐색** — 예: 로드 직후 리페이터/스크롤뷰 콘텐츠에 1px 패딩을 넣었다 빼 `EffectiveViewportChanged`를 유도. 저비용이면 유력하나 재현 확인 필요.
4. **조건부 적용** — `AdoptHandle` 직전 `tab.Items.Count == 0`(직전이 빈 폴더)일 때만 언스틱 로직 실행 → 일반 경로 비용 0. 단, 작동하는 언스틱 방법(1~3)이 전제.
5. (비선호) **P1 롤백/동기 로드** — 타이밍으로 회피되나 대형 폴더 프리즈(P1 목적) 재발. 채택 안 함.

### 재디버깅용 계측 스니펫 (임시로 다시 넣을 때)
`NexaFileGrid`:
```csharp
public string DebugRealization()
{
    var v = Repeater.ItemsSourceView;
    return $"viewCount={(v?.Count ?? -1)} elem0={Repeater.TryGetElement(0) is not null} "
         + $"vpH={BodyScroll.ViewportHeight:F0} extH={BodyScroll.ExtentHeight:F0} "
         + $"off={BodyScroll.VerticalOffset:F0} repH={Repeater.ActualHeight:F0}";
}
```
`MainWindow.LoadDirectory`(채택 직후) — `%LOCALAPPDATA%\NexaDir\debug.log`에 기록:
```csharp
var dbg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexaDir");
Directory.CreateDirectory(dbg);
var f = Path.Combine(dbg, "debug.log");
File.AppendAllText(f, $"{DateTime.Now:HH:mm:ss.fff} LOAD {(left?"L":"R")} path={path} direct={result.DirectCount} liveCount={tab.Items.Count} {grid.DebugRealization()}\n");
grid.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
    File.AppendAllText(f, $"{DateTime.Now:HH:mm:ss.fff}   post liveCount={tab.Items.Count} {grid.DebugRealization()}\n"));
```

---

## BUG-002 · 이름변경이 드래그/우클릭 시 오발동 (☐ 수정 예정)

- **심각도**: 중 · **상태**: ☐ B-8로 수정 예정
- **증상**: 선택된 항목을 드래그하려 하면 인라인 이름 변경이 시작됨. 우클릭에도 이름변경 트리거가 관여.
- **원인**: 이름변경 트리거가 `PointerPressed`(캡처 시점)에서 발동 → 드래그 시작 press에도 걸림. 우클릭 press도 선택/트리거 경로를 탐.
- **수정 방향**: 트리거를 **PointerReleased(드래그 없이 클릭 완료)** 로 이동 + 우클릭(우버튼) 조기 제외. 드래그 시작 시 pending 취소. (원장 B-8)

## BUG-003 · desktop.ini 더블클릭 "Attempted to perform an unauthorized operation" (✅ 해결)

- **심각도**: 중 · **상태**: ✅ 해결(2026-07-04 15:28, `8b891c0`) — 실기 QA 대기
- **원인**: `StorageFile.GetFileFromPathAsync`/`Launcher` 브로커가 시스템/숨김 파일(desktop.ini)에 대해 unauthorized.
- **해결**: `ActivateItem` 파일 실행을 **`Process.Start(UseShellExecute=true)`(ShellExecute)** 로 교체 — 셸 기본 프로그램 위임, 더 많은 형식 처리. Alt+↓ 파일 실행(BUG/요청)도 동일 경로로 해소.

## BUG-004 · 비활성 패널 클릭(행/빈 영역) 시 포커스 이동 안 됨 (✅ 해결)

- **심각도**: 중 · **상태**: ✅ 해결(2026-07-04 15:30, `ba0249f`) — 실기 QA 대기
- **증상**: 탭 클릭은 패널 활성화되나, 포커스 없는 패널의 행/빈 공간 클릭은 활성 패널이 안 바뀜.
- **해결**: `OnRootPointerPressed`가 좌/우클릭 시 포인터가 놓인 패널(`PanelUnderPointer`)을 활성화(handledEventsToo).

## BUG-005 · 탭 영역 더블클릭 새 탭 이름 공백 (✅ 해결)

- **심각도**: 낮음 · **상태**: ✅ 해결(2026-07-04 15:28, `8b891c0`) — 실기 QA 대기
- **원인**: `AddTab`이 `Nav.NavigateTo`만 하고 `Title` 미설정 → 첫 전환 전까지 공백. 이동 후에야 `Navigate`가 Title 설정.
- **해결**: `AddTab`에서 생성 즉시 `tab.Title = PathDisplay.TabTitle(basePath)`.

## BUG-006 · 다른 패널→자기 패널 붙여넣기 시 다른 패널 미갱신 (☐ watcher)

- **심각도**: 중 · **상태**: ☐ B-12w(watcher)로 근본 해결. 임시: 작업 후 양쪽 패널 재로드.
- **증상**: 자기 패널 복사·붙여넣기는 자동 갱신되나, 다른 패널이 같은 폴더를 보고 있을 때 그쪽은 반영 안 됨.
- **원인**: 파일 작업 후 **작업 패널만** 재로드. 외부/타 패널 변경 감시 부재.
- **해결 방향**: 파일시스템 변경 감시(Pub/Sub watcher)로 영향 폴더를 보는 모든 패널/탭 자동 갱신 + 수동 새로고침(F5). 성능은 경로 구독 범위·디바운스로. (원장 B-12w · [TODO](TODO.md) C-3)
