# feat/bottom-panel · 진행 로그 (하단 패널 구현)

> 브랜치 `feat/bottom-panel`(분기: main `bd45f86`, 3차 감사 병합 직후)에서 일어난 변화만 시간순 기록.
> 설계 근거: [docs/03 §3-K2b 임베디드 터미널](../03-features.md) · 레이아웃 [docs/20](../20-ui-layout.md).

## 목표

하단 도킹 패널을 **placeholder → 실제 동작**으로. 설계(FR-K2b): 하단 도킹·다중 탭·경로 동기(cd)·
ConPTY 호스팅·크래시 격리·코어 `pty` 이식 추상화. 하단 패널은 여러 콘텐츠(정보/미리보기/Hex/터미널)를
호스팅하는 컨테이너로 설계.

## 현재 상태 (시작 시점)

- **구현됨**: 레이아웃(`TerminalPanel` Row5, 좌/우 도킹 Border, 상하·좌우 스플리터+자석 스냅), 표시/분리 토글
  (`OnToggleTerminal`·`UpdateBottomDock` — 하단 분리는 듀얼일 때만).
- **미구현(placeholder)**: 도킹 콘텐츠는 "정보/미리보기/Hex/터미널 (placeholder)" 텍스트뿐. 실제 콘텐츠·상호작용 없음.

## 분해 (수직 슬라이스)

| ID | 단위 | 상태 |
| --- | --- | --- |
| **BP-1** | **패널 컨테이너 프레임워크** ← 첫 슬라이스(사용자 선택) | 착수 예정 |
| BP-2 | 정보/미리보기 뷰(선택 항목 속성·간단 미리보기) | 예정 |
| BP-T1 | 터미널 스파이크 — 코어 `pty`(Rust, ConPTY) + C ABI | 예정 |
| BP-T2 | 터미널 에뮬레이터 컨트롤(VT/xterm) — **방식 나중 결정**(라이브러리 vs 커스텀) | 예정 |
| BP-T3/T4 | 경로 동기(cd)·다중 탭·크래시 격리 | 예정 |

## BP-1 세부 계획 (착수 예정)

1. **콘텐츠 호스트**: 도킹 Border를 스왑 가능한 콘텐츠 호스트로. 콘텐츠 종류 `BottomPanelKind`(Info/Preview/Hex/Terminal)
   + 선택 UI(탭 또는 셀렉터). 재사용 컨트롤은 수요 확인 후(우선 앱 내부).
2. **토글**: 표시/숨김 단축키 **Ctrl+\`**(설계 명시) — 기존 `OnToggleTerminal` 재사용.
3. **높이·표시 세션 저장**: 하단 패널 높이+표시 상태를 `session.json`(SessionStore)에 저장/복원(SessionState 확장).
4. **활성 경로 컨텍스트**: 활성 패널의 현재 폴더를 하단 패널에 전달(터미널 cd·정보 뷰용) — 속성/이벤트.

## 결정 사항

- 첫 슬라이스: **BP-1 컨테이너**(구조·낮은 위험) — 사용자 확정.
- 터미널 에뮬레이터 방식(라이브러리 vs 커스텀 VT): **BP-T1 시점에 별도 결정**(퍼미시브 라이선스·맥 빌드/이식·WinUI 통합 검토).
- ConPTY 위치: 정석은 코어 `pty`(이식 격리) — BP-T1에서 확정.

<!-- 진행마다 아래에 6하원칙 항목 append -->

## BP-1 · 2026-07-05 · 패널 컨테이너 프레임워크 (착수·구현)

- **BP-1a 콘텐츠 호스트** `(커밋)`: placeholder → 재사용 `BottomDockView`([.xaml](../../app/Nexa.App/BottomDockView.xaml)/[.cs](../../app/Nexa.App/BottomDockView.xaml.cs)). 콘텐츠 종류 선택(정보/미리보기/Hex/터미널, 라디오식) → 콘텐츠 스왑. 지금은 **정보만 실제**(현재 폴더), 나머지 "준비 중". 좌/우 도킹에 각각 배치. `MainWindow.RefreshBottomDocks`가 각 패널 현재 폴더를 `InfoText`로 전달(네비 시 갱신).
- **BP-1b Ctrl+\` 토글** `(커밋)`: `VK_OEM_3`+Ctrl → `ToggleBottomPanel`(토글 버튼 상태 뒤집기 + 기존 표시 경로). 설계 FR-K2b.
- **BP-1c 세션 저장/복원** `(커밋)`: `BottomPanelState`(표시/높이/좌우 분리/콘텐츠 종류)를 `session.json`에 저장·복원. 토글·분리·종류 변경 시 `MarkDirty`, 시작 시 `RestoreBottom`(높이는 토글 후 복원, 종류는 `Enum.IsDefined` 검증). 왕복 실증(session.json `Bottom` 섹션 기록 확인).
- **검증**: 앱 빌드 0/0 · 시작 스모크 · 세션 왕복. **실기 QA 대기**: Ctrl+\` 토글·콘텐츠 종류 전환·재시작 시 상태 복원.
- **남음(BP-1d)**: 정보 콘텐츠를 현재 폴더 외 **선택 항목 속성**(크기/날짜/속성)으로 확장 — BP-2와 함께.

### 다음
- **BP-2**: 정보/미리보기 뷰 실제 콘텐츠(선택 항목 속성·간단 미리보기).
- **BP-T1**: 터미널 스파이크(코어 `pty` ConPTY) — 에뮬레이터 방식은 이 시점 결정.
