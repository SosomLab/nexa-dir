# refactor/002-audit · 진행 로그 (시간순 · 6하원칙)

> **이 문서는 `refactor/002-audit` 브랜치에서 일어난 변화만** 시간순(YYYY-MM-DD HH:MM:SS, KST)으로 기록한다.
> 각 항목은 **6하원칙**(누가/언제/무엇을/왜/어디서/어떻게)으로, **요청 → 진행 방향 → 목표 → 기능 → 세부 구현 → 파일:줄** 순서로 남긴다.
> 진행하는 틈틈이 **맨 끝에 append**한다(새 커밋마다 항목 추가). 시각은 커밋 committer date 기준.
> 이전 라운드: [refactor-001-worklog.md](refactor-001-worklog.md) (감사 → C1 코어 트리 이관 → 성능·정리, `0.1.0`→main 병합).

## 브랜치 개요

- **브랜치**: `refactor/002-audit` (일련번호식 — 다음 라운드는 `refactor/003-…`)
- **분기 기준**: `b38e6b3` (main, C1 병합 직후 · 태그 `0.1.0`은 병합 전 베이스라인 `6e81734`)
- **목적/요청**: 사용자 — "**전체 점검용** 브랜치에서 **설계/개발에 대한 검증을 다시 시작**." (1차 감사 이후 C1 병합된 현재 상태 기준으로 문서·소스·진행 정합성 재점검.)
- **선택된 진행 방향**: 통합 감사(E1) 완료 → **트랙 A 성능(P1/P2/P3) 즉시 착수**(사용자 지정), 이후 트랙 B(구조)·C(설계 계약)·D(배포/NFR 재보정)·E(퀵윈).

## 1차(001) 이후 이월된 후보 (재점검 시 우선 확인)

> 아래는 001 감사에서 식별됐으나 001 라운드에서 미착수로 이월된 항목. 재감사에서 유효성·우선순위 재평가.

- **C1** `bool left` 좌/우 이중화 ~20개 메서드 관통 → `PanelView` 객체 통합 (001 감사 "최고 가치", A2 MVVM 이음새).
- **A2** `Nexa.ViewModels` 부재 — 로직 대부분 `MainWindow.xaml.cs` 코드비하인드(맥 테스트 불가).
- **B1** FFI 패닉 언와인딩 가드 부재(`panic=abort`/`catch_unwind`).
- **B2** CI가 P/Invoke 왕복 미검증(dll 로드/마샬 실행 안 함) → 헤드리스 스모크.
- **B4** 심링크 metadata follow 의미 혼재(깨진 링크 `attrs=0`).
- **B5** 빌드 취약: TFM 두 csproj 중복(`Directory.Build.props` 부재)·`..\..\core` 하드코딩·arm64 크로스 미비.
- **B6** cargo-deny end-to-end 미검증.
- **D2** `docs/19` 다수 기능 커밋 해시 `(이 단위)` 미기입.
- **(신규 후보)** 탭별 스크롤 위치 기억(UX) · 펼침/접힘 범위 diff 통지(현재 Reset+오프셋 복원).

## 진행 ↔ 커밋 계층 매핑 (요약)

```
refactor/002-audit  (분기: b38e6b3)
├─ E1 통합 감사(5축 병렬) ......... 5b18bcb
│    └ 산출: docs/journal/20260702_234558_refactor-002-audit.md
├─ E2 트랙 A-3 [P3] 경로→NodeId ... 8db50c3 (+ faef3cc 테스트 픽스)
├─ E3 트랙 A-1 [P1] 열거 백그라운드 . 4c6f74c
├─ E4 트랙 E 문서 스테일 일괄+ADR등재 . 6db4915
├─ E5 P1 실기 QA#1 — GoUp 스크롤 수정 . cfecd64
├─ E6 P1 QA#2 — 빈폴더 blank 버그 등록 . 6245622
├─ E7 트랙 A-2 [P2] 펼침/접힘 범위 diff . 19e8c79
├─ E8 트랙 B-2a PanelView 그룹 객체 .... 6f000cb
├─ E9 트랙 B-1 Nexa.ViewModels + 테스트 . 31da09e (+62543c3 크로스플랫폼 픽스)
├─ E10 전체 문서 최신화(세션 진행분 반영) . f76e0ba
├─ E11 트랙 A-4 [P6] 아이콘 캐시·로딩 큐 ... (이 커밋)
└─ E12~ 트랙 B-3(PanelControl)·C~ ...... 예정
```

## 진행 현황 체크리스트 (트랙별)

> 2차 감사 우선순위 백로그([20260702_234558_refactor-002-audit.md](20260702_234558_refactor-002-audit.md) §★) 기준 전체 목록과 진행 여부. ✅ 완료 · ☐ 미착수 · 🚧 부분.

### 트랙 A — 성능
- [x] **A-1 [P1]** 열거·펼침 백그라운드화(UI 프리즈 제거) — E3
- [x] **A-2 [P2]** 펼침/접힘 Reset→범위 diff + 캐시 시프트 — E7
- [x] **A-3 [P3]** 코어 경로→NodeId 조회(per-row 마샬 제거) — E2
- [x] **A-4 [P6]** 아이콘 LRU 캐시 + 속도 제한 로딩 큐(화면밖 드롭) — E11
- [ ] **A-5 [P5]** arena 노드 회수 + 유휴 RSS 트림 자니터(soak 전)

### 트랙 B — 구조 리팩토링
- [x] **B-2a** `PanelView` 그룹 객체로 `bool left` 이중화 소거 — E8
- [~] **B-1** `Nexa.ViewModels`(net8.0) + C# 테스트 — E9 (**부분**: TabTitle·NavigationHistory·IconKey 이관; 로직 대부분 아직 MainWindow)
- [ ] **B-3** `PanelControl` UserControl로 XAML 좌/우 복붙 제거

### 트랙 C — 설계 계약 동결 (M1 마감 전, 전부 미착수)
- [ ] **C-1** nexa-ops 파일작업 엔진 설계+크레이트(에러/취소/진행률 표준 SSOT)
- [ ] **C-2** VFS Provider 계약(list/stat/read/watch) + 트리 Provider 경유(M4 전)
- [ ] **C-3** watcher 설계(부분 무효화·선택 pruning 훅)
- [ ] **C-4** 에러 모델 표준(코어→호스트 코드 enum + last-error)
- [ ] **C-5** csbindgen 도입(수동 P/Invoke 미러 대체, M2/M3 전)

### 트랙 D — 배포·NFR 재보정 (전부 미착수)
- [ ] **D-1** NFR-M2/P5 목표 실측 기반 재보정(soak/R2R Windows 실측 선행)
- [ ] **D-2** 배포 이원화(fwk-dependent MSIX + 포터블 폴더 zip), 단일 exe 검증
- [ ] **D-3** 상주 규율 구현(캐시 캡·EmptyWorkingSet 트림·압박 구독)
- [ ] **D-4** "네이티브 준함" 서술 정정

### 트랙 E — 저위험 퀵윈
- [x] **E-4** 문서 스테일 일괄 + ADR-0004 등재 — E4 (+ E10 전체 최신화)
- [ ] **E-1** interop 죽은 `nexa_dir_*` 제거
- [ ] **E-2** `panic="abort"`(FFI 언와인딩 가드)
- [ ] **E-3** 심링크 `symlink_metadata` + attrs 유닛테스트
- [ ] **E-5** docs/19 커밋해시 백필(플레이스홀더 21개)

### 별도 (감사 외 · QA/사용자 요청)
- [~] **BUG-001** 빈 폴더 이탈 시 목록 blank — 재현 안 됨(모니터링, [BUGS.md](../BUGS.md))
- [ ] **UX** 탭 복귀 시 스크롤/선택 유지(활성 탭 재클릭은 맨 위) — 사용자 요청, 접수
- [ ] **실기 QA 회귀 점검** — 트랙 A/B가 네비·펼침·아이콘 경로 다수 변경(뒤로/앞으로/위로·탭·펼침 일괄 확인 권장)

---

## E1 · 2026-07-02 · 통합 감사(전체 개발 범위 종합 점검) → `(이 커밋)`

- **누가/왜**: 사용자 요청 — "1차 감사 기준 차용해 전체 개발 범위 점검"(Q1 성능·용량 네이티브 준함 / Q2 미래기능 설계 반영 / Q3 요구 상충·아키텍처 변경 필요 / Q4 기능단위 구조·리팩토링 + 누락 설계·설계 훼손). 추가 지침: ①1차 완료/취소/정리 항목 제외하고 2차 기준 수립 ②C# UI를 Double Commander(네이티브)와 속도·메모리·크기 비교.
- **어떻게**: `general-purpose` 에이전트 **5축 병렬**(성능·최적화 / 설계 완결성·미래수용성 / 기능단위 구조·리팩토링 / 문서·요구 정합성 / Double Commander 비교) → 심각도·질문축별 통합.
- **핵심 결론**: 큰 아키텍처 변경 불필요, 설계 훼손 없음(C1으로 개선). 지금 손볼 3대 = ①성능 UI 병목(P1/P2/P3) ②구조 리팩토링(ViewModels/PanelView/XAML, C3 전) ③설계 계약 공백(ops/VFS/watcher/에러표준). Double Commander 대비 UI는 네이티브 열위(메모리 2배·크기 3–5배·콜드스타트) → 스택 교체 아닌 NFR 재보정+배포 이원화+상주규율 구현으로 대응.
- **산출**: [20260702_234558_refactor-002-audit.md](20260702_234558_refactor-002-audit.md) — 2차 기준(트랙 A~E 우선순위 백로그) 포함.

## E2 · 2026-07-02 · 트랙 A-3 [P3] — 코어 경로→NodeId 조회(per-row 마샬 제거) → `(이 커밋)`

- **왜**: 감사 P3 — `ExpandPaths`(F18)·`IndexOfPath`(GoUp)가 경로 매칭을 위해 가시 행마다 `TreeGetRow`+`TreeRowPath` P/Invoke + 문자열 복사(O(경로수×가시행)). 큰 폴더 진입/위로 이동 시 마샬링·할당 폭주.
- **무엇 · 파일**:
  - 코어 [nexa-tree](../../core/crates/nexa-tree/src/lib.rs): `index_of_path(&str)`(가시 목록 경로 매칭, 끝 구분자·ASCII 대소문자 무시) + `expand_path(&str)`(경로로 가시 폴더 펼침). 단위테스트 `index_of_path_and_expand_path`.
  - ABI [nexa-interop](../../core/crates/nexa-interop/src/lib.rs): `nexa_tree_index_of_path`/`nexa_tree_expand_path`(→`NexaRange`), **ABI v4→v5**. 라운드트립 테스트 추가.
  - 관리형 [NativeInterop](../../app/Nexa.App/NativeInterop.cs): `TreeIndexOfPath`/`TreeExpandPath` + `ExpectedAbi=5`.
  - 컬렉션 [VirtualTreeCollection](../../app/Nexa.App/VirtualTreeCollection.cs): `IndexOfPath`=코어 단일 호출, `ExpandPaths`=경로당 단일 `TreeExpandPath`(전체 재스캔 제거).
- **효과**: 경로 매칭이 O(경로수×가시행) per-row 마샬 → **경로당 단일 P/Invoke**(매칭은 코어 Vec 스캔). 스모크 출력 `abi=5`.
- **검증**: 코어 `cargo test` green(nexa-tree 10 + interop 5, 경로 라운드트립 포함), fmt·clippy(-D warnings). 앱부는 PR CI(app) + 실기 QA.

## E3 · 2026-07-03 · 트랙 A-1 [P1] — 열거·펼침 백그라운드화(UI 프리즈 제거) → `(이 커밋)`

- **왜**: 감사 P1 — `LoadDirectory`가 `TreeOpen`(전체 열거+`metadata()` syscall+정렬) + 펼침 재적용을 **UI 스레드 동기 실행** → 수만 항목 폴더(System32·node_modules) 진입/이동 시 UI 프리즈(NFR-P1 <150ms · NFR-R5 무블록 위반).
- **무엇 · 파일**:
  - 컬렉션 [VirtualTreeCollection](../../app/Nexa.App/VirtualTreeCollection.cs): 동기 `Open`/`ExpandPaths` 제거 → **정적 `OpenAndExpand`**(새 핸들에서 열거+깊이순 펼침, 별칭 없어 스레드 안전, 반환=(핸들, 펼침 전 직접 자식 수)) + **`AdoptHandle`**(UI 스레드에서 이전 핸들 Close·새 핸들 채택·Reset).
  - 윈도우 [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs): `LoadDirectory`를 `async void`로 전환 — `Task.Run(OpenAndExpand)`로 오프로드, `await` 연속(UI 스레드)에서 채택. **패널별 세대 가드**(`_leftLoadGen`/`_rightLoadGen`)로 로드 중 재이동 시 이전 결과 폐기(뒤늦은 핸들 정리). `Navigate`에 `onLoaded` 연속 콜백 추가 → 로드 후 동작(**GoUp** 대상 선택·포커스, **경로바 파일** 선택)을 완료 시점으로 이동. `Navigate`/호출부는 동기 유지(async 파급 최소화).
- **효과**: 열거·펼침이 UI를 블록하지 않음(대형 폴더 진입 프리즈 제거). 로드 중 이전 핸들이 계속 표시돼 **빈 화면 깜빡임 없음**. 재이동 시 최신 이동만 반영. 헤더에 "여는 중…" 표시.
- **한계(후속)**: 뒤처진 로드는 결과만 폐기(백그라운드 열거는 완료까지 진행) — 코어 취소 가능 열거는 별도 슬라이스. `Task.Run` 결과 폐기 시 CPU 낭비는 남음.
- **검증**: 로컬 `dotnet build`(app x64) green(경고 0/오류 0). 코어 무변경. 앱부 재진입·GoUp 순서·프리즈 해소는 **실기 QA**(대형 폴더 진입, 빠른 연속 이동, 위로 이동 후 대상 선택) + PR CI(app).

## E4 · 2026-07-03 · 트랙 E-4 — 문서 스테일 일괄 최신화 + ADR-0004 결정기록 등재 → `(이 커밋)`

- **왜**: 감사 D1(스테일 다발·001 이월·악화) + 문서우선 규약 위반(C1 결정 ADR-0004가 docs/10 미등재). 001 병합·002 진행(P3·P1)으로 지표가 다시 벌어짐 → 공개 전환 전 신뢰성 확보.
- **무엇 · 파일**(현재 ground truth 재수집 후 반영):
  - [STATUS](../STATUS.md): 갱신일 07-03, C1 **완료·main 병합**(`b38e6b3`, 태그 `0.1.0`) + 2차 감사 라운드(P3 ABI v5·P1 백그라운드 열거) 반영. MainWindow **721→970줄** 정정. 코어 **19→21 tests**. C1 "다음 단위"를 완료+후속 트랙으로 갱신.
  - [16 구조](../16-project-structure.md): 크레이트 트리에 **`nexa-tree` 추가**·`nexa-vfs` "스텁"→실내용·`VirtualTreeCollection.cs` 추가. 요약 **3 crates/9 tests → 4 crates/21 tests**, ABI v5. MainWindow/interop 설명 현행화.
  - [19 구현현황](../19-implemented-features.md): 코어 테스트 **17→21**(크레이트별 내역 명기, 문서 내부 상충 해소).
  - [02 로드맵](../02-roadmap.md): **M0 체크박스 전부 완료 표시** + csbindgen "함수 왕복"을 **수동 P/Invoke 현실**로 정정(csbindgen 이월 명기).
  - [10 결정기록](../10-decision-record.md): **§1-1 ADR 색인 신설** — ADR-0001~0004 등재, **ADR-0004(코어 트리/선택 모델) Accepted**(구현·병합 완료) 기록.
- **한계(후속 E5)**: docs/19 `(이 단위)` 커밋해시 플레이스홀더 **21개 백필**(D2, git 아카이브 매핑 필요)은 별도 단위로 이월.
- **검증**: 문서 편집만(코드 무변경). ADR 링크 파일명 실재 확인(`22-adr-0003-view-and-panel-modules.md` 등). 코어/앱 빌드 영향 없음.

## E5 · 2026-07-03 · P1 실기 QA#1 — 상위 이동 시 대상 스크롤 안 됨 수정 → `(이 커밋)`

- **증상(QA)**: 긴 목록(System32 4880개)에서 하단 폴더 진입 후 Alt+↑ 상위 이동 시, 대상이 선택은 되나 뷰가 **최상단에 걸림**(스크롤이 대상으로 안 감). 들쭉날쭉(가끔 가운데, 대개 상/하단).
- **원인**: P1으로 로드가 비동기가 되며 `AdoptHandle`(Reset) 직후 `onLoaded`→`SelectByPath`→`ScrollIndexIntoView`가 같은 틱에 실행. 이때 `ItemsRepeater`가 새 목록을 아직 레이아웃하지 않아 `ScrollableHeight==0` → `ChangeView(target)`가 **0으로 클램프**돼 최상단. 동기 경로에선 입력 이벤트 후 레이아웃이 Low 콜백 전에 끝나 문제없었음.
- **무엇 · 파일**: [NexaFileGrid](../../app/Nexa.Controls/NexaFileGrid.xaml.cs) `ScrollIndexIntoView` — 스크롤 직전 **`BodyScroll.UpdateLayout()`로 레이아웃 동기 강제** → 가상화 익스텐트(StackLayout 추정=전체개수×평균높이) 즉시 확정 후 오프셋 계산·`ChangeView`. 익스텐트가 여러 패스 필요하면 자라는 동안만 재시도(끝 근처 항목은 상한 클램프=하단 완전표시가 최선). [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs)는 임시 지연 되돌리고 주석만 정리.
- **효과**: GoUp(F13-1)·경로바 파일 선택(F23-1) 등 로드 후 스크롤 대상이 안정적으로 위치. 실기 QA 통과(사용자 확인).
- **검증**: 로컬 `dotnet build`(app x64) green. 실기 QA — 상위 이동 유지 동작 확인.

## E6 · 2026-07-03 · P1 실기 QA#2 — 빈 폴더 blank 버그 등록(미해결) → `(이 커밋)`

- **증상(QA)**: 빈(0개) 폴더 진입 후 상위/뒤로 이동 시 대상 목록이 빈 화면(헤더 개수는 정상). 한 번 발생하면 이후 이동도 고착.
- **진단**: 임시 계측(`DebugRealization` + debug.log)으로 확인 — `viewCount=3`인데 `elem0=False`(요소 미실체화), `extH` 고착. **ItemsRepeater `EffectiveViewport` 고착**이 근본 원인(빈 소스 후 뷰포트 무변화 → 재실체화 미트리거). P1 비동기 로드가 노출.
- **시도·실패**(모두 되돌림): `UpdateLayout` 강제 / `ItemsSource` null 토글 재바인딩 / `Repeater.Visibility` 토글 / `ScrollViewer.Content` detach·reattach. 마지막은 **시작 시 2–5초 행 + 간헐 크래시** 부작용까지 유발. 상세·근거·후보 해결책은 [BUGS.md](../BUGS.md) **BUG-001**.
- **결정**: 반복 중단(사용자 지시). 실험 코드/계측을 마지막 정상 상태([cfecd64])로 되돌리고, 시도 기록을 BUG-001에 상세 정리(직접 디버깅용). 스크롤 수정(E5)은 유지·정상.
- **무엇 · 파일**: [BUGS.md](../BUGS.md) 신설(BUG-001). 코어/앱 코드 변경 없음(되돌림 완료).

## E7 · 2026-07-03 · 트랙 A-2 [P2] — 펼침/접힘 범위 diff 통지(전체 Reset 제거) → `(이 커밋)`

- **왜**: 감사 P2 — 인라인 펼침/접힘이 `InvalidateAndReset`(캐시 clear + `Reset`)으로 통지해 **뷰포트 전 행 재실체화 + 아이콘 전량 재로드 + 스크롤 맨 위 튐**(E18 오프셋 복원 핵으로 완화 중) → 60fps 붕괴. 코어는 이미 `TreeRange`(Start/Removed/Inserted) diff를 주는데 C#이 버림.
- **무엇 · 파일**:
  - [VirtualTreeCollection](../../app/Nexa.App/VirtualTreeCollection.cs) `ToggleExpand`: 코어 diff를 살려 **범위 `Add`/`Remove` 통지**(`RaiseChange`) + `_cache`·캐럿 **인덱스 시프트**(`ApplyDiff`). 위쪽(< start) 행은 유지 → 재실체화·아이콘 재로드 없음. `CountOnlyList`(개수만, 행 미실체화)로 **대형 펼침도 무블록**. `InvalidateAndReset` 제거.
  - 디스클로저 글리프는 **항상 토글** → **자식 0개 빈 폴더도 펼침/접힘 표시**(조기반환 회귀 수정, 실기 QA 지적). 가시 행 변경이 있을 때만 시프트+통지.
  - [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs) `ToggleExpandRow`: **E18 오프셋 캡처/복원 핵 제거**(더는 안 튐).
- **효과**: 펼침/접힘 시 위쪽 행·아이콘·스크롤 위치 보존, 대형 폴더도 무블록. **실기 QA 통과**(접기/펴기·스크롤 유지·빈 폴더 확장 확인).
- **부수 확인 — BUG-001 재현 안 됨**: clean 빌드(cfecd64+A-2)에서 **빈 폴더 이탈 시 blank 미발생**. 이전 blank는 모두 실험 코드(UpdateLayout/null토글/RefreshRealization) 켜진 빌드에서만 관찰 → 그 실험들이 원인이었을 가능성. BUG-001을 "재현 안 됨(모니터링)"으로 갱신([BUGS.md](../BUGS.md)).
- **검증**: 로컬 `dotnet build`(app x64) green. 코어 무변경. `ItemsRepeater` 범위 Add/Remove가 개수 통지로 정상 실체화됨(#1 미지수 해소, `CountOnlyList` 유효).

## E8 · 2026-07-03 · 트랙 B-2a — PanelView 그룹 객체로 `bool left` 이중화 소거 → `(이 커밋)`

- **왜**: 감사 B-2(1차 C1 이월·악화) — 좌/우 패널 동작이 `left ? _leftX : _rightX`로 관통(25곳), 상태·UI가 쌍 필드로 흩어짐(god object 원인). C3(교차선택 UI) 얹기 전 최저비용 정리. 이후 B-1/B-3·C3의 이음새.
- **무엇 · 파일**: [PanelView](../../app/Nexa.App/PanelView.cs) 신설 — 패널 하나의 상태(Tabs/Active/LoadGen) + UI 참조(Grid/Header/PathBar/TabStrip)를 묶음. [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs): 쌍 필드(`_leftTab`/`_rightTab`·`_leftTabs`/`_rightTabs`·`_leftItems`/`_rightItems`·`_leftLoadGen`/`_rightLoadGen`) 제거 → `_left`/`_right` **PanelView 2개** + `Panel(bool left)` 헬퍼. 모든 `left ? _leftX : _rightX` → `Panel(left).X`(25곳). ctor에서 XAML 요소로 PanelView 구성.
- **성격**: **무동작 변경**(순수 배선 정리). 컴파일로 검증, 잔존 쌍 참조 0 확인.
- **효과**: `bool left` 분기 소거, 패널 상태 응집 → 가독성·안전성↑. B-1(ViewModels)·B-3(PanelControl)·C3의 기반.
- **검증**: 로컬 `dotnet build`(app x64) green(경고 0/오류 0). 코어 무변경. 실기 스모크(양 패널 네비/탭 전환/펼침/선택 회귀) 권장.

## E9 · 2026-07-03 · 트랙 B-1 — Nexa.ViewModels(순수 로직) 추출 + C# 테스트 도입 → `(이 커밋)`

- **왜**: 감사 B-1(1차 A2 이월) — 순수 로직이 MainWindow 코드비하인드에 묶여 **C# 테스트 0**(WinUI는 맥 빌드 불가). god object 축소 + 크로스플랫폼 테스트 확보.
- **무엇 · 파일**:
  - [Nexa.ViewModels](../../app/Nexa.ViewModels/) 신설(**net8.0**, 플랫폼 무관): `PathDisplay.TabTitle`(경로→표시명) + `NavigationHistory`(뒤/앞 스택+현재, F13 순수화).
  - [Nexa.ViewModels.Tests](../../app/Nexa.ViewModels.Tests/) 신설(xUnit): **12 테스트**(TabTitle 6 · NavigationHistory 6) 통과 — 프로젝트 첫 C# 단위 테스트.
  - [PanelTab](../../app/Nexa.App/PanelTab.cs): `Current`/`Back`/`Fwd` 필드 → `NavigationHistory Nav` + `Current` 읽기 패스스루(읽기 지점 무변경).
  - [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs): `Navigate`/`GoBack`/`GoForward`를 `Nav` 위임 + 공통 종단 `ShowCurrent`로 정리. `UpdateNavButtons`는 `Nav.CanGoBack/CanGoForward`. 로컬 `TabTitle` 제거 → `PathDisplay.TabTitle`.
  - CI: **`viewmodels` 잡 추가**(ubuntu-latest, `dotnet test`) — 크로스플랫폼 C# 로직 게이트. [docs/18](../18-build-and-test.md)·[docs/16](../16-project-structure.md) 갱신(SSOT 규약).
- **효과**: 순수 로직 맥/Linux/Windows 테스트 가능(첫 C# 테스트 12개). MainWindow 네비게이션 단순화(3메서드→위임+종단 1). B-3·C3의 기반.
- **검증**: `dotnet test app/Nexa.ViewModels.Tests` 12/12 green(로컬). `dotnet build app/Nexa.App`(x64) green. 앱부 네비(뒤로/앞으로/위로)·탭 회귀는 CI(app) + 실기 QA.
- **CI 캐치(B-1 가치 입증)**: 최초 push에서 `viewmodels` 잡(ubuntu) 4개 실패 — `Path.GetFileName`이 **리눅스에선 `\`를 구분자로 취급 안 함** → Windows 경로에서 폴더명 추출 실패(Windows에선 통과라 숨어있던 버그). `TabTitle`을 구분자(`\`·`/`) 직접 처리로 수정(플랫폼 무관). 크로스플랫폼 테스트가 실제 결함을 잡음.

## E10 · 2026-07-03 · 전체 문서 최신화(세션 진행분 반영) → `(이 커밋)`

- **왜**: 이번 세션에서 트랙 A(P1/P2/P3)·B-2a·B-1 완료 + 새 프로젝트(`Nexa.ViewModels`/`.Tests`)·CI 잡·C# 테스트 도입 → STATUS/CLAUDE/구조 문서가 다시 스테일. 공개 전환 대비 정합성 유지.
- **무엇 · 파일**:
  - [STATUS](../STATUS.md): 002 라운드 꼬리에 A-2(P2)·B-2a·B-1·C# 테스트(12)·BUG-001 반영. "다음 단위" C1 후속을 A/B 완료·B-3 예정으로. MainWindow 970→**955줄**.
  - [CLAUDE.md](../../CLAUDE.md): 현 단계 "스캐폴딩·M0" → **M1 진행(M0 완료·0.1.0)+2차 감사 트랙 A/B 완료**. 리포 구조에 `Nexa.Controls`/`Nexa.ViewModels(+Tests)` 추가, docs 00~**31**. §8 다음 단계 현행화(B-3→트랙 C/D).
  - [16 구조](../16-project-structure.md): MainWindow 955줄·`PanelView` 추가, 970 정정.
  - [19 구현현황](../19-implemented-features.md): C# 테스트(`dotnet test`, xUnit 12) 요약 추가.
  - (docs/18은 B-1에서 이미 갱신 — viewmodels 잡·§2-2.)
- **성격**: 문서만. 코드/빌드 무변경.
- **검증**: 스테일 잔존 스캔(970·"M0 진행") 정리 확인.

## E11 · 2026-07-03 · 트랙 A-4 [P6] — 아이콘 LRU 캐시 + 속도 제한 로딩 큐(화면밖 드롭) → `(이 커밋)`

- **왜**: 감사 P6 — `LoadIconAsync`가 행 실체화마다 셸 썸네일 호출(캐시·상한·취소·동시성 제한 전무). 큰 폴더/빠른 스크롤 시 셸 호출 폭주·중복(같은 확장자 반복)·낭비(이동 후에도 옛 폴더 로드 진행).
- **무엇 · 파일**:
  - [IconKey](../../app/Nexa.ViewModels/IconKey.cs)(순수, net8.0): 캐시 키 — 폴더=`dir`·확장자없음=`file`·일반=소문자 확장자·**앱별 고유 아이콘 확장자(exe/lnk/ico…)=경로 키**. 구분자 직접 처리(크로스플랫폼). 테스트 [IconKeyTests](../../app/Nexa.ViewModels.Tests/IconKeyTests.cs) **13개**(총 C# 25).
  - [ShellIconCache](../../app/Nexa.App/ShellIconCache.cs)(앱): LRU`<key,ImageSource>`(상한 256, NFR-M2) + **속도 제한 로딩 큐** — 요청은 큐 적재, `DispatcherQueueTimer`(80ms)가 **동시 상한 4** 내에서만 꺼내 로드. 캐시 히트 즉시, 미스는 큐잉. 이미지(사진)·아이콘(exe/파일형식/폴더) **모두 렌더**(빈 썸네일만 스킵).
  - [NexaFileGrid](../../app/Nexa.Controls/NexaFileGrid.xaml.cs): `ItemsRepeater` 요소 수명(`ElementPrepared`/`ElementClearing`)을 `RowRealized`/`RowRecycled` 이벤트로 노출(도메인 비종속). x:Bind 템플릿은 DataContext 미설정 → **인덱스+요소맵**으로 항목 식별.
  - [MainWindow](../../app/Nexa.App/MainWindow.xaml.cs): `LoadIconAsync`·`WireTab` 제거 → 그리드 `RowRealized`→`Request`, `RowRecycled`→`Cancel`(화면 밖 행은 큐에서 제거). `_iconCache`를 UI `DispatcherQueue`로 ctor 생성.
- **효과**: 같은 종류 아이콘 1회 셸 호출·공유(중복 제거), **동시/신규 셸 호출이 상한을 절대 안 넘음**(빠른 스크롤에도 안정), 화면 밖 행 드롭, LRU 메모리 상한. exe/바로가기 고유 아이콘 유지.
- **QA 여정(중요)**: ① 세마포어版(무한 대기열) → System32 **빠른 스크롤 시 네이티브 크래시**(crash.log 없음=관리 예외 아님). ② 요소 수명 취소만으론 부족(진입/취소 폭주·이미 시작된 셸 호출). ③ **속도 제한 로딩 큐**(사용자 제안, Explorer/Finder식 "스크롤 정착 후 로딩")로 동시·신규 호출을 하드 상한으로 묶어 해결. 또한 `Type==Image` 필터 탓에 exe/타입 아이콘이 안 보이던 것 → 아이콘 타입도 렌더하도록 수정.
- **검증**: `dotnet test` **25/25** green(IconKey 13 크로스플랫폼). `dotnet build`(app x64) green. **실기 QA 통과** — 빠른 스크롤 크래시 없음 + 아이콘 정상 표시(사용자 확인).

<!-- 진행마다 아래에 6하원칙 항목 append -->
