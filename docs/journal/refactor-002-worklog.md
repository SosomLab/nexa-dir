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
├─ E4 트랙 E 문서 스테일 일괄+ADR등재 . (이 커밋)
└─ E5~ 트랙 A-2 [P2]·트랙 B~ ...... 예정
```

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

<!-- 진행마다 아래에 6하원칙 항목 append -->
