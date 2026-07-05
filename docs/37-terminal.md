# 37 · 임베디드 터미널 — ConPTY + VT 에뮬레이터 (BP-T)

> 하단 패널 "터미널" 탭에서 셸(cmd/powershell/pwsh)을 구동한다. 설계 근거: [03 §3-K2b](03-features.md)·ADR-0003 [22](22-adr-0003-view-and-panel-modules.md).
> 상태: **BP-T1 토대 구현**(ConPTY 세션 + 기본 입출력·lazy). VT 파서/화면 버퍼/렌더는 후속 슬라이스.

## 1. 구조

```
TerminalView (UI)  ──입력(키)──▶  ConPtySession.Write  ──▶  셸 stdin
     ▲                                                          │
     └── 출력(텍스트)  ◀── ConPtySession.Output ◀── 셸 stdout(ConPTY, VT 바이트)
```

- **`ConPtySession`**([app/Nexa.App/Terminal/ConPtySession.cs](../app/Nexa.App/Terminal/ConPtySession.cs)): Windows **ConPTY**(`CreatePseudoConsole`)로 셸을 구동.
  파이프로 stdin/stdout 연결, 백그라운드 읽기 루프가 UTF-8 디코드해 `Output` 발생, `Write`로 입력 전송, `Resize`로 크기 변경, `Dispose`로 종료.
  기본 셸 = **pwsh → powershell → cmd** 순 자동 선택.
- **`TerminalView`**([app/Nexa.App/Terminal/TerminalView.xaml.cs](../app/Nexa.App/Terminal/TerminalView.xaml.cs)): 출력 표시 + 키 입력(문자·Enter·방향키 VT 시퀀스·Ctrl+글자) 전송, 크기→열/행 근사 후 `Resize`.
- **호스트**: `BottomDockView`의 "터미널" 종류. **lazy** — 터미널 탭이 실제 활성화될 때만 `TerminalView` 생성·세션 시작. 이후 탭 전환에도 세션 유지, 패널/창 닫힘(Unloaded) 시 종료. 작업 디렉터리 = 활성 패널 현재 폴더(`CurrentFolder`).

## 2. Lazy 로딩 (요구)

- 터미널 탭을 **누르기 전엔 세션이 생성되지 않는다**(셸 프로세스 없음). 처음 활성화 시 1회 생성·시작.
- 활성화 이후엔 **세션 유지**(다른 탭으로 갔다 와도 그대로). 종료는 패널/창 닫힘 또는 명시적 정리.

## 3. 슬라이스 (증분)

| # | 내용 | 상태 |
| --- | --- | --- |
| **BP-T1** | ConPTY 세션 + 기본 입출력(평문) · lazy | ✅ |
| **BP-T2** | **VT 파서 + 화면 버퍼 + 색 렌더**([VtScreen.cs](../app/Nexa.App/Terminal/VtScreen.cs)) — 셀 그리드(문자·전경/배경색·굵게/반전), CR/LF/BS/HT, SGR(16/256/트루컬러), 커서 이동(CUP/CU\*/CHA/VPA), 지우기(ED/EL), 줄 스크롤(스크롤백). 라인=색 run(Border 배경+TextBlock 전경)으로 렌더(throttle 33ms) → **oh-my-posh 배경색·ls 색·`clear`·개행 정상** | ✅ |
| BP-T3 | 렌더 성능(Win2D/가상화)·커서 표시·선택·**Nerd Font**(파워라인 글리프) | 예정 |
| BP-T4 | 다중 터미널 탭·셸 선택 설정·크래시 격리(NFR-R3)·코어 `pty` 이관(이식) | 예정 |

> **작업 디렉터리**: 셸 (재)시작 시점의 **활성 패널 선택 탭 폴더**(`WorkingDirectoryProvider`). 시작 후 폴더 변경/탭 이동은 무영향.
> **셸 종료(exit)**: 화면을 리셋하고 **새 셸을 자동 재시작**(패널/창 닫힘 제외). 
> **글리프 주의**: 파워라인/Nerd Font 아이콘은 Consolas에 없어 네모로 보일 수 있다(색은 정상) — Nerd Font 설정은 BP-T3.

## 4. 성능·안정 (NFR)

- 출력 버퍼 상한(현재 뒤 200KB). 스크롤백/링버퍼는 BP-T3.
- 셸 프로세스는 자식(수명 분리), `Dispose`에서 `TerminateProcess`+핸들 정리. 장기: 크래시 격리(별도 프로세스 그대로) · 다중 세션 상한.
- **이식**(장기): ConPTY는 Windows 전용 — 비Windows는 OS별 PTY로 추상화(코어 `pty` 모듈, ADR-0003). 현재는 C#(앱)에서 ConPTY 직접.
