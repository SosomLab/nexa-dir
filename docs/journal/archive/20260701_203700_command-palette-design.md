# 작업 기록 — 2026-07-01 20:37 (KST)

> 기록 ID: `20260701_203700_command-palette-design`
> 이전 기록: `20260701_203125_foldersfirst-sort-option`

## 1. 요청
- 나중에 **커맨드 팔레트** 형태로 기능을 사용할 수 있도록 **목록(백로그)에 등록**하고, **JSON 기반 설정과 연동**되도록 **설계**.

## 2. 판단
- 커맨드 팔레트 개념은 이미 여러 문서에 산재(FR-I2·FR-J4·[02]·[03]·[09]·[10]) — "단일 액션 레지스트리" 전제.
- 없던 것 = ① **전용 설계 문서**, ② **JSON 설정 연동 설계**(이번 신규 요청 핵심).
- 앞서 만든 [Settings.cs](../../app/Nexa.App/Settings.cs)(`AppSettings.Sort.FoldersFirst`)가 설정 스키마의 **첫 입주자** → 팔레트/명령이 이 값을 읽고/바꾸는 구조로 연결.

## 3. 산출 (설계 + 백로그 등록)
- **[26-command-palette.md](../26-command-palette.md) 신설**:
  - **명령 레지스트리(SSOT)** 우선 — 메뉴·툴바·단축키·컨텍스트·런처·플러그인·팔레트가 공유(§1~2).
  - 팔레트 UX(§3): `Ctrl+Shift+P`, 접두 모드(`>`/`/`/`@`/`?`), 퍼지·랭킹(MRU/빈도)·인자 입력.
  - 키맵 해석(§4): 기본 키 + `keybindings.json` 재정의.
  - **JSON 설정 연동(§5, 핵심)**: `settings.json`/`keybindings.json`/`commands.user.json`/`state.json` 4파일, 양방향(읽기+즉시 저장), `AppSettings`→`SettingsStore` 확장 경로, 마이그레이션(schemaVersion).
  - 스택 매핑(§6): 레지스트리·키맵·퍼지·직렬화는 UI 비종속 순수 로직 → 맥 단위테스트 가능, 팔레트 UI만 Windows.
  - 단계적 구현(§7): α 레지스트리+팔레트 UI(M1 토대, 메뉴/툴바를 레지스트리 구동으로 전환) → β JSON 설정 연동 → γ 접두 모드·사용자 명령·플러그인 기여.
- **백로그/링크 등록**: [19](../19-implemented-features.md) 트랙 D 행 + 설정 시스템 노트, [02](../02-roadmap.md) M1/M6 링크, [05](../05-requirements.md) FR-I2 링크, [STATUS](../STATUS.md) 문서 인덱스.

## 4. 검증
- 문서 전용 변경(코드 없음). 앱 빌드 영향 없음.

## 5. 다음
- 트랙 D 착수 시 α(레지스트리+팔레트 UI) → 메뉴/툴바 실동작을 레지스트리로 통합.
- 설정 시스템(JSON) 구현과 β 동반: `keybindings.json` 재정의 + `sort.foldersFirst` 영속화(F15 후속) + 열린 목록 재정렬.
