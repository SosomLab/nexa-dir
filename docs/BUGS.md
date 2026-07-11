# BUGS — 알려진 이슈 / 버그 백로그

> 미해결 버그를 추적한다. 해결 시 커밋 해시와 함께 "해결" 표기 후 하단 이동/삭제.
> 형식: 증상 → 재현 → 근본 원인(확인 방법) → 시도한 해결책과 결과 → 후보 해결책.

---

## BUG-010 · 포터블/설치본(self-contained 게시) 결함 2건 — 렌더링 깨짐 + 시작 크래시 (✅ 해결)

- **심각도**: 치명(0.3.2 릴리스 자산 사용 불가) · **리포트**: 2026-07-11 사용자("포터블 다운로드 받아서 실행하면 깨져서 실행됨" — 전 행이 `Nexa.App.DirItem` 텍스트).
- **상태**: **✅ 해결**(2026-07-11, `fix/portable-render-xbf` → 0.3.3). dev(FD)·CI 빌드는 무관 — **self-contained 게시 산출물에서만** 발생해 실기 QA 전까지 미발견.

### 결함 A — 파일 목록 렌더링 깨짐 (모든 행이 클래스명 텍스트)
- **원인**: PKG-2의 loose XAML 폴백이 라이브러리 **소스 `.xaml`** 을 게시 루트에 배치 → `LoadComponent`가 소스를 런타임 파싱하면서 **`x:Bind`(컴파일 바인딩)가 죽어** 행 템플릿이 `ToString()` 폴백으로 표시. 컬럼 헤더도 소실.
- **해소**: 게시 루트에 **컴파일된 `.xbf`**(Nexa.Controls obj 산출)를 배치(`PublishLooseXamlAssets` 개편 + 미존재 시 빌드 에러 게이트) + 게시 디렉터리 클린 산출(스크립트). 스크린샷 검증.

### 결함 B — 시작 중 간헐 크래시 0xC0000374 (힙 손상, 30~60%)
- **원인(덤프 2단 분석으로 확정)**: WER 풀 덤프는 CoreCLR JIT의 delete에서 *탐지*만 보여줌(탐지자≠범인) → **PageHeap(full)** 재현으로 범인 즉발 — **WinAppSDK 1.6 MRT Core `Microsoft.Windows.ApplicationModel.Resources!GetDefaultPriFile`** 이 `ResourceManager` 생성 중 힙 손상(자사 이슈 #4191 계열, self-contained 미패키지 한정). 환경(경로 등)에 따라 확률 변동 — `data\` 폴더 유무로 보였던 초기 패턴은 우연.
- **해소**: **WindowsAppSDK 1.6.\* → 1.8.\*** 업그레이드(전 csproj 4종). PageHeap ON 3/3 + 일반 5/5 + Debug(FD) 스모크 green. 이 PC 런타임 1.8 기설치 확인.

### 교훈
- self-contained 게시는 **FD와 다른 런타임 바이너리**를 번들 → 배포 자산은 반드시 **산출물 자체로 실행 QA**(빌드 green≠동작).
- 힙 손상은 탐지 스택을 범인으로 단정하지 말 것 — PageHeap으로 손상 지점을 직접 잡는다.

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

## BUG-007 · 내장 터미널 캐럿(커서) 미표시 (☑ 해결)

- **심각도**: 중 · **상태**: ☑ 해결(2026-07-06). 관련 [docs/37](37-terminal.md).
- **증상**: 터미널 탭에서 셸이 동작하지만 **커서(캐럿)가 화면에 보이지 않는다**(입력 위치 파악 어려움).
- **원인**: `VtScreen`은 커서 위치(`_cx/_cy`)를 유지하지만 `TerminalView` 렌더가 **커서 셀을 별도로 그리지 않음**(라인 색 run만 렌더). 커서 표시/깜빡임 미구현.
- **해결**: `VtScreen`에 커서 좌표 노출 API(`CursorCol`/`CursorRow`/`ScrollbackCount`) 추가. `TerminalView`에 캐럿 오버레이(`CaretLayer` Canvas) 도입 — 셀 그리드 위에 블록 캐럿을 겹쳐 그린다. **포커스 시 반투명 accent 채운 블록 + 깜빡임(530ms)**, **비포커스 시 외곽선(중공)** 으로 포커스 여부도 시각화. 출력·입력 활동 시 캐럿 즉시 표시. 포커스 진입/이탈(`GotFocus`/`LostFocus`)로 깜빡임·스타일 전환.

## BUG-008 · 내장 터미널 일부 SGR 색 미반영(명령 미리보기 등) (◐ faint 해결, 잔여 후속)

- **심각도**: 중 · **상태**: ◐ **faint(예측) 해결(2026-07-07)**, 일부 확장 SGR/글리프는 후속(BP-T3). 관련 [docs/37](37-terminal.md).
- **증상**: PSReadLine **명령 미리보기(예측 입력)** 등에서 쓰는 색(흐린 회색)·일부 스타일이 실제 터미널과 다르게 보인다.
- **원인**: SGR 중 **faint(2, 흐리게)** 를 렌더에 미반영(파서는 플래그만 보관). 일부 확장/드문 시퀀스 미처리. 파워라인 글리프는 별개(Nerd Font 문제).
- **해결(faint)**: `TermCell.Faint` 필드 추가 → `Put`에서 셀에 보관, `SameStyle`에 포함, `TerminalView` 렌더에서 `Opacity=0.45`로 표시. PSReadLine 인라인 예측(history)이 **VS Code처럼 연한 회색 미리보기**로 보인다.
- **해결 방향(잔여)**: 미처리 확장 SGR/CSI 보강. (파워라인 글리프는 Nerd Font 적용 — 별도.)
- **보강(07-08)**: ECH에 이어 **SU/SD(CSI S/T)·IND/NEL(ESC D/E)·CNL/CPL·DECSTBM(CSI r, 영역 스크롤)** 구현 — 프롬프트 중복·스크롤 후 커서 행 어긋남 해소. 위치 정확도의 최종 원인은 VT가 아니라 **레이아웃 반올림 누적**(journal 07-08)이었음.

## BUG-009 · 외부(탐색기→앱) 드래그가 금지 커서로 표시 (☑ 해결 — 상승 프로세스 OLE 드롭 폴백)

- **심각도**: 중 · **상태**: ☑ **해결(2026-07-07)** — 고전 OLE `IDropTarget` 폴백(`OleDropTarget.cs`)으로 실기 복사 확인. 관련 [docs/33](33-file-ops-dnd-design.md) DND-EXT.
- **증상**: DND-EXT 구현(`09ec2b2`) 후에도 **탐색기에서 앱으로 파일 드래그 시 빨간 금지 표시**. 드롭 불가. 앱→탐색기(밖으로)는 정상.
- **원인(확정)**: **이 PC는 UAC 꺼짐(`EnableLUA=0`) → 탐색기에서 실행해도 모든 프로세스가 상승(High IL)** 으로 뜬다(진단 로그 `startup elevated=True`로 실증). WinUI 3(XAML)의 드롭 수신은 WinRT 드래그 스택(`CoreDragDropManager` 계열)을 경유하는데, 이 스택이 **IL 일치 여부와 무관하게 "프로세스 토큰이 상승이면" 수신을 거부**한다(탐색기도 High IL인 UAC OFF 환경에서도 차단). XAML `DragEnter/DragOver` 자체가 미도달 → `DoDragDrop`이 `DROPEFFECT_NONE` → 금지 글리프. 앱 코드(`ExternalDragOp` 판정)는 문제 없음. 밖으로 드래그는 탐색기의(정상) 드롭 타깃이 받으므로 무관.
  - 정확히 같은 재현: [microsoft-ui-xaml#10119](https://github.com/microsoft/microsoft-ui-xaml/issues/10119)(`EnableLUA=0` → 금지 커서·DragEnter 미도달, `EnableLUA=1`+재부팅으로 해소 확인). 추적: [microsoft-ui-xaml#7690](https://github.com/microsoft/microsoft-ui-xaml/issues/7690)(Backlog, 2022~미해결, MS 확인 코멘트) · [WindowsAppSDK#3921](https://github.com/microsoft/WindowsAppSDK/issues/3921)/[#4433](https://github.com/microsoft/WindowsAppSDK/issues/4433). **패키징 여부 무관**(MSIX인 Files·Windows Terminal도 동일 — Files는 관리자 실행 시 DnD 비활성+안내 배너).
  - **이전 "일반 권한 재현이라 UIPI 배제" 판단은 무효** — UAC OFF PC에선 탐색기 실행도 상승이므로 "일반 권한" 조건 자체가 성립하지 않았다. (docs/33 함정 기록의 상위 일반화: **상승 = 인바운드 DnD 차단**, UAC ON/OFF 불문.)
- **진단(임시 코드 — 확인 후 제거됨)**: `%TEMP%\nexa-dnd-debug.log` — ① 시작 마커(`elevated=True` 실증) ② 창 루트 DragEnter 프로브(외부 드래그 시 미기록 = XAML 아래 계층 차단 확정) ③ `DragOver formats` 판정 로그.
- **해결(채택: OLE 폴백)**: **상승 감지 시** XAML 콘텐츠 브리지 HWND(`DesktopChildSiteBridge`)의 (무력한) XAML 드롭 타깃을 `RevokeDragDrop`으로 걷어내고 자체 **고전 Win32 OLE `IDropTarget`**(`OleDropTarget.cs`) 등록 — 같은 IL 간 고전 OLE는 차단되지 않는다(Double Commander `uOleDragDrop.pas`가 같은 방식으로 UAC OFF에서 정상인 것으로 실증). 화면좌표→XAML DIP 역변환+히트테스트로 폴더 행/패널 현재 폴더 판정, 연산 규칙(내부=`DragOp`·외부=복사 기본/Shift=이동)과 전송 엔진(`TransferPathsInto`)은 XAML 경로와 동일. 이동은 앱이 직접 수행(최적화 이동)하므로 원본에 None 보고(중복 삭제 방지). **비상승 실행은 미등록**(XAML 경로 정상 → 이중 처리 방지). 상승 시 내부(패널 간) 드래그도 같은 폴백으로 함께 복구.
- **잔여 한계**: **UAC ON에서 명시적 "관리자 권한으로 실행"**(Medium IL 탐색기 → High IL 앱)은 고전 OLE 자체가 교차 IL을 차단 → 폴백으로도 불가(탐색기 동급 제한). 필요 시 Files식 안내 후속.
- **검토 메모**: 대안이던 UAC 켜기(#10119 확인 픽스)·Files식 DnD 비활성+안내는 미채택. `ChangeWindowMessageFilterEx(WM_DROPFILES…)`는 무효(UIPI 메시지 필터 문제가 아님 — OLE/COM 콜백 + WinRT 스택의 명시적 상승 검사).
