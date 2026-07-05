# 작업 기록 — 2026-07-05 14:27:04 (KST)

> 기록 ID: `20260705_142704_new-menu-datecol-session`
> 이전 기록: `refactor-003-worklog` · 브랜치: `refactor/003-audit`

## 1. 요구 (이번 세션)
1. 개발환경 설치 완료 확인 → 빌드까지 검증.
2. 빈영역 우클릭 "새로 만들기"(폴더/파일/바로가기) 구현.
3. 수정 날짜/시간 컬럼 — DateTime/Date/Time 3종, 기본은 DateTime만 표시.
4. 탭 세션(열린 탭·정렬·펼침) 저장 — 별도 파일, I/O 최소화 + 유휴 활용 + 급종료 대비.
5. 저장 요청/수행 분리(Tick당 1회) 기법 도입.
6. 이 PC(빌드 가능 머신) 기억 + 식별 방법 문서화. 진행 정리·설정 저장 매커니즘 기술.

## 2. 개발환경
- 실측: rustc/cargo 1.96.1 · .NET SDK 9.0.315 · VS Build Tools 2022(MSVC) · winget · pwsh 7.6.3. Node 미설치(M6 전 불필요).
- 코어 `cargo build` green · 앱 `dotnet build app/Nexa.App` 0/0. **이 PC(DESKTOP-O7JCBAT)에서 풀 빌드·실행 가능** 확인.

## 3. 구현
- **BG-N1/N2/N3 새로 만들기**(빈영역): 폴더/빈 파일(.txt)/바로가기(.lnk). `.lnk`는 `IShellLinkW`+`IPersistFile` COM([ShellLink.cs](../../app/Nexa.App/ShellLink.cs))+대상 파일 선택기. 생성→재로드→선택→스크롤(오프스크린 강제 실체화)→**즉시 인라인 이름변경**. 중복 이름 " (2)" 회피.
- **COL-D1/D2 수정 날짜/시간 컬럼**: `DirItem`에 라벨 3종(`ModifiedDateTimeLabel`=yy/MM/dd HH:mm · `ModifiedDateLabel`=yy/MM/dd · `ModifiedTimeLabel`=HH:mm). 기본 컬럼을 `yyyy-MM-dd`→**DateTime(yy/MM/dd HH:mm)** 교체(헤더 "수정한 날짜/시간", 폭 130). Date/Time 개별 컬럼+가시성 토글은 후속(COL-D3/D4 — 현 컬럼 시스템에 가시성 개념 없음).
- **SESS 탭 세션 저장/복원**([SessionStore.cs](../../app/Nexa.App/SessionStore.cs)): 별도 파일 `session.json` @ `%LOCALAPPDATA%\NexaDir\`. 저장 대상=활성 패널·패널별(활성 탭·열린 탭[경로·펼침·정렬]). 시작 시 복원(존재 폴더만·활성 탭만 즉시 로드). 정렬은 현 패널 단위→각 탭 동일 기록(스키마는 탭 단위).
  - **요청/수행 분리 + Tick 코얼레싱**: `MarkDirty()`=dirty set(요청) · 단일 Tick(1s)이 dirty 1회 소비(수행)→**Tick당 최대 1회 저장**. 유휴 실행(Low) · 해시 무변경 스킵 · 원자적 쓰기(temp→교체) · 안전주기(60틱) 자가치유 · 종료 flush(Closed·ProcessExit, 급종료 대비).
  - 실증: 삭제→기동(기본)→정상종료 flush→session.json 생성 확인 · 복원 경로 재기동 정상.

## 4. 검증·정리
- 로컬 스모크: 앱 기동 크래시 없음 · `CloseMainWindow()` 정상 종료 · session.json 스키마 확인.
- **CI**: WinUI 앱 job **green**. Rust job이 fmt로 실패 → 원인은 기존 TA 커밋의 rustfmt 누락(제 C# 변경 무관). `cargo fmt` 정리(clippy -D/test green 확인) 후 push → Rust 게이트 복구.

## 5. 문서
- **docs/34**(신규): 설정·세션 영속화 — 저장 매커니즘/위치/데이터 범위(session.json 상세 + settings/state.json 관계).
- docs/26 §5-1 표에 `session.json` 행 추가. docs/11 §4-6 이 PC 식별 방법(MachineGuid/HW UUID/호스트명).

## 6. 커밋(브랜치 refactor/003-audit)
- `1f1f92d` 날짜컬럼(COL-D1/D2) · `2a2c0ed` 새로만들기+세션 · `1e59fd5` cargo fmt · `db75331` docs/11 §4-6.

## 7. 다음
- COL-D3/D4(Date/Time 선택 컬럼 + 가시성 토글) · YYYY/MM/DD HH:MM:SS 전체 포맷 옵션.
- 새로만들기·날짜컬럼·세션 복원 **실기 QA**(Windows). 셸 컨텍스트 통합(SHELL) 별도 ADR.
