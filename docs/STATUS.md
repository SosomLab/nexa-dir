# STATUS — Nexa Dir 진행 현황

> 갱신: 2026-07-03 (KST) · 단계: **M1 진행** — 데이터흐름(F1~5)+레이아웃(F6~9)+**플래그십 초안**(인라인 펼침·컬럼 F10 / 선택 F11·F17) + **패널 탭**(F20·닫기 F22) + **계층 경로 바 `NexaPathBar`**(F23) + 네비(F13·F19·F21) + **숨김/점 파일 토글**(코어 attrs·ABI v2, F24) + **마우스 뒤로/앞으로**(F25) 구현(**F1~F25**, [19](19-implemented-features.md)). 기본 폰트 Segoe UI. **진행 중: C1 코어 트리/선택**(`refactor/001-audit`, ADR [29](29-adr-0004-core-tree-model.md)) — 슬라이스 1 `nexa-tree` 모델(F26) + 슬라이스 2 **C ABI `nexa_tree_*`·ABI v3**(F27). + 슬라이스 3a **호스트 ABI 안전 계층**(로드 시 버전·레이아웃 검사, F28, 감사 A2/A3 정정). + 슬라이스 3b **앱 가상화 재배선**(F18 펼침유지·스크롤 위치 복원) + 슬라이스 **4-1 AC5 10만 노드 코어 벤치**(expand 100k 5.7ms 등 전부 프레임 예산 안 → O(log n) 매핑 불요). + **4-2 탭별 트리 핸들 캐시**(전환 시 재-Open 제거) + **4-3 id→가시 인덱스 조회**(`nexa_tree_index_of`, **ABI v3→v4**) — 클릭 시 O(n) 행 실체화 병목 제거. + **E17 죽은 코드 정리** + **4-4 펼침/접힘 스크롤 보존** + **E19 키보드 캐럿 스크롤 견고화**(오프스크린 추적). **C1 완료 → `refactor/001-audit`를 main에 병합**(`b38e6b3`, 태그 `0.1.0`=머지 전 베이스라인 `6e81734`). **현재: 2차 감사 라운드 `refactor/002-audit`** — 통합 감사(전체 개발범위 5축 점검, [journal](journal/20260702_234558_refactor-002-audit.md)) 후 **트랙 A 성능** 완료: **A-3 [P3] 경로→NodeId 조회**(`nexa_tree_index_of_path`/`expand_path`, **ABI v4→v5**, per-row 마샬 제거) + **A-1 [P1] 열거·펼침 백그라운드화**(`Task.Run` 오프로드+세대 가드 — 대형 폴더 진입 UI 프리즈 제거, NFR-P1/R5) + **A-2 [P2] 펼침/접힘 범위 diff 통지**(전체 Reset·재실체화 제거, 캐시 인덱스 시프트) + **A-4 [P6] 아이콘 LRU 캐시 + 속도 제한 로딩 큐**(`IconKey`+`ShellIconCache` — 화면밖 드롭, 빠른 스크롤 크래시 해소). **트랙 B 구조 리팩토링**: **B-2a `PanelView` 그룹 객체**(`bool left` 이중화 25곳 소거) + **B-1 `Nexa.ViewModels`(net8.0, 플랫폼 무관) 추출**(`PathDisplay`·`NavigationHistory`·`IconKey`) + **C# 테스트 도입**(xUnit **25**, CI `viewmodels` 잡=ubuntu 크로스플랫폼). [BUG-001](BUGS.md) 빈 폴더 이탈 blank는 **✅ 해결**(실험 코드가 원인, 되돌린 뒤 미재현·QA 확인). **테스트: 코어 21(+벤치 1 ignored) · C# 25 green.** MainWindow 955줄(B 리팩토링 지속 대상)

## 1. 확정된 결정 (Decision Record [10](10-decision-record.md))

| # | 영역 | 결정 |
| --- | --- | --- |
| DR-1 | 기술 스택 | **Rust 코어(cdylib) + WinUI 3(C#/.NET8)** · ADR-0001 Accepted |
| DR-2 | 디자인 | **프로툴 고밀도(Path Finder풍)**, 다크 기본, 키보드 우선 |
| DR-3 | 배포 | **MSIX + Releases + winget** + 포터블(폴더/단일exe) 가능 설계 |
| DR-4 | AI | 보류 (M5 전 별도 ADR) |
| DR-5 | 라이선스 | **소스공개 제한형 PolyForm Noncommercial**(대안 BSL), **공개 예정(현재 private, 진행 후 public 전환)**, 개인무료/상업유료 |

## 2. 핵심 요구 (확정)

- ★ **플래그십**: macOS식 인라인 폴더 확장 + **폴더 교차 다중 선택** + 혼합 일괄 작업 (M1)
- **좌/우 듀얼 패널 + 패널별 싱글/멀티라인 탭** (M1)
- **상단 계층 경로 바(브레드크럼)** — 우선 구현 (M1)
- **컨텍스트 메뉴**: Windows 셸 메뉴 호스팅 + 고유 항목 병합 (M1)
- **퀵 런처 바**(외부 터미널/에디터, M1) · **하단 임베디드 터미널 패널**(ConPTY, 설계동결/구현후속)
- **상주 규율**: 저메모리·주기 정리·무간섭·오류 격리 (전 단계 NFR)
- **클라우드/네트워크**(SMB→SFTP→S3→SaaS, M4, 단계적)
- **플러그인**(WASM+Python/Node RPC, M6) · **내장 zip**(향후 지원)
- **라이선스 정품 인증**(오프라인 1차/온라인 2차, Ed25519, M7) → [17](17-licensing-activation.md)

## 3. 마일스톤

- **M0** 기반: 인터롭 PoC, 스트리밍 열거, 가상화 렌더, CI/부트스트랩
- **M1** ★ 1순위 묶음: 계층 경로 바 · 탭/듀얼 · **플래그십(인라인 트리+교차선택)** · 컨텍스트 메뉴 · 퀵 런처
- **M2** 미리보기 · **M3** 검색 · **M4** 클라우드/원격 · **M5** AI · **M6** 플러그인 · **M7** 라이선스 인증

## 4. 개발 모델 ([11](11-dev-environment.md))

- 코어(Rust): **맥에서 개발/테스트 가능** · 앱(WinUI): **Windows 필수**(PC/VM/CI).
  실측: **dotnet은 맥 가능, WinUI 3는 맥 불가**(XAML 컴파일러 Windows 전용) → docs/11 §6-1.
- 환경 설치: **macOS=`bootstrap.sh`(brew)** · **Windows=`bootstrap.ps1`(choco→winget→수동)**, 도구 표 docs/11 §4-5.
- 다른 PC: clone → bootstrap → 빌드 · CI(windows-latest)가 신뢰 원천.

## 5. 개발 방식 ([15](15-dev-methodology.md))

- **작은 기능 단위(수직 슬라이스) 순차** · **단위=커밋 1개** · **초안→확장 프로토타이핑** · main 항상 green.
- 단위 백로그(M0→M1)는 docs/15 §7. 커밋은 Conventional Commits.

## 6. 진행 현황 / 다음 단계

- OD2 AI(보류), 상표 출원 검토. LICENSE(영문/한글)·THIRD-PARTY-NOTICES ✅.
- **스캐폴딩 + 환경 검증 완료** ✅: `core/`(nexa-core/vfs/interop) · `app/Nexa.App`(WinUI) · CI(mac/win) · LICENSE · bootstrap(sh/ps1) · global.json/.vscode.
  - Windows 풀빌드 실측(2026-06-30): Rust 1.96 + VS Build Tools 2022 + .NET SDK 9 + Windows App Runtime 1.6. `cargo test` + `dotnet build` green. 도구 상세 [11](11-dev-environment.md).

- **M0 진행 — 데이터 흐름 수직 슬라이스 완성** ✅ (코어 → 인터롭 → C# → UI):

  | # | 단위 | 산출 | 검증 | 커밋 |
  | --- | --- | --- | --- | --- |
  | 1 | 인터롭 왕복 PoC | `nexa_poc_add` ↔ C# P/Invoke + csproj cargo 통합 | dll 왕복(5/42) · CI success | `c41dc41`·`af07fee` |
  | 2 | 로컬 스트리밍 열거 | `nexa-vfs::read_dir_entries`(점진 Iterator, 오류 격리) | 7 tests green | `623da9d` |
  | 3 | 인터롭 디렉터리 열거 API | `nexa_dir_open/next/close`+`NexaEntry`(핸들 스트리밍) | 9 tests green | `541e887` |
  | 4 | 디렉터리 열거 C# 바인딩+UI | `ReadDir` → MainWindow 목록 표시 | 빌드 0/0 · 앱 기동 | `7e12c1f` |
  | 5 | ItemsRepeater 가상화 렌더 | `ListView`→`ScrollViewer`+`ItemsRepeater`(트리 토대) | 빌드 0/0 · 앱 기동 | `1a14150` |
  | 6 | 레이아웃 골격(F6) | 7행 그리드(메뉴·툴바·런처·좌/우 듀얼·하단 도킹·상태바) + `GridSplitter` 크기조절 + 숨김 토글 + 영역 색상 구분(CommunityToolkit Sizers) | 빌드 0/0 · **CI success** | `105d9e8`…`597e52e` |

- **레이아웃·플래그십 초안 진행** ✅ (F7~F17, 상세 [19](19-implemented-features.md)):

  | 묶음 | 구현 |
  | --- | --- |
  | 레이아웃 정교화 (F7~F9) | 좌/우 듀얼 목록·하단 도킹 연동(F7) · **`NexaFileGrid` 재사용 컨트롤 추출**(F8, 독립 제품 [21](21-adr-0002-fileview-control.md)) · 스플리터 얇게+**자석 스냅**(F9) |
  | ★ 플래그십 초안 (F10·F11·F17) | **인라인 폴더 펼침 + Finder식 4컬럼**(F10) · **파일 선택 단일/Ctrl 다중/Shift 범위**(F11) · **키보드 범위·비연속(Ctrl+Space)·→/← 펼침·캐럿**(F17) |
  | 네비/메뉴/UX (F12~F16) | 초박형 커스텀 **`NexaMenuBar`**(F12) · **패널별 네비 뒤로/앞으로/위로**(F13) · Explorer식 선택·호버·클릭 즉시반응(F14) · 폴더우선 정렬 옵션화(F15) · 리사이즈 커서·키보드·탭 활성(F16) |
  | 트리/키보드 확장 (F17-1·F18·F19) | **←로 상위 폴더 이동**(F17-1) · **펼침 상태 유지**(경로 기준, 진입/이동에도 동일, F18) · **Alt+↓ 활성화**(폴더 진입/파일 실행, F19) |
  | 탭 (F20·F22) | **패널별 탭**(멀티라인·고정크기·`…`·더블클릭 추가·활성 상단 하이라이트, `PanelTab` 탭별 경로/기록/펼침, F20) · **탭 닫기**(Ctrl+W·탭 더블클릭 + "더블클릭 동작" 설정, F22) |
  | 네비게이션 (F13·F19·F21) | **패널별 뒤로/앞으로/위로**(F13, **위로 시 자기 선택** F13-1) · **Alt+↓ 활성화**(폴더 진입/파일 실행, F19) · **Alt+↑/←/→ 위로·뒤로·앞으로**(F21, 빈 폴더 포함 F21-1) |
  | 계층 경로 바 (F23) | **`NexaPathBar`**(Nexa.Controls) — 세그먼트 클릭 이동(현재 무동작·드라이브 `C:`→`C:\`·hover 반전)·간격 축소·**우클릭 텍스트 편집**(전체선택·복붙·Enter 이동·Esc/포커스아웃 복귀)·**파일 경로면 상위 폴더+파일 선택**(F23-1) |
  | 표시/설정 (F24) | **숨김 파일 보기 · 점(.) 파일 보기**(표시(S) 메뉴 체크형 토글 2개, 독립·동시 설정, **기본 둘 다 표시=체크 ON**, 해제 시 감춤) — 숨김 판정을 **Rust 코어 `attrs`**(ABI v2, 추가 syscall 0)로, 점 파일은 이름으로. `NexaMenuEntry` 체크형 지원 추가 |
  | UX (폰트) | **기본 폰트 = Segoe UI**(App 리소스), 아이콘=Segoe MDL2 · 탭 폰트=12(메뉴와 동일) |

  | 이동 (F25) | **마우스 뒤로/앞으로 버튼**(XButton1/2) → **포인터가 놓인 패널** 뒤로/앞으로(네비 바 버튼과 동일, 빈 폴더 포함) |

  단축키는 현재 하드코딩 → **명령 레지스트리+`keybindings.json` 재정의**로 이관 예정([26 §5-4](26-command-palette.md), FR-I2). **입력 제스처 설계**(기능당 다중 바인딩·키보드+마우스): [26 §2-1](26-command-palette.md).

  기능별 구현·테스트 방법 → [19](19-implemented-features.md) · 빌드/실행 → [18](18-build-and-test.md) · 레이아웃 [20](20-ui-layout.md).

- **빌드 이슈 해결**(2026-07-01): Sizers TFM 불일치(CS0234)로 F6가 Windows에서 빌드 실패 → 앱 TFM `19041→22621` 정합(`2bf0089`).
  방지 규약(맥은 WinUI 빌드 불가 → **CI green 확인 필수**, TFM 정합) → [18](18-build-and-test.md) §2 · CLAUDE.md §6.

- **다음 단위(M0→M1)**:
  1. **코어 `VisibleRow` 평면 스트림(C1)** ✅ **완료·main 병합** — 인라인 펼침·선택 진실원천을 **Rust 코어(`nexa-tree`)로 이관**(가시행 스트림 + OrderedSet 선택, [07](07-flagship-tree-multiselect.md)·ADR [29](29-adr-0004-core-tree-model.md)). 슬라이스 1~4(모델 F26·C ABI F27·호스트 ABI 안전계층 F28·앱 재배선 F18·10만 노드 벤치·핸들캐시·죽은코드 정리) 전부 완료. **후속(2차 감사 `refactor/002-audit`)**: 트랙 A 성능 — P3(ABI v5)·P1(백그라운드 열거)·P2(범위 diff 통지)·**P6(아이콘 캐시·로딩 큐)** **완료**(남음 P5 트림). 트랙 B 구조 — B-2a(`PanelView`)·B-1(`Nexa.ViewModels`+C# 테스트) **완료**, **다음 B-3(`PanelControl` XAML dedup)**. 이후 트랙 C 설계 계약(ops/VFS/watcher/에러표준). 트랙별 전체 현황 → [journal 워크로그 §진행 현황 체크리스트](journal/refactor-002-worklog.md).
  2. **교차폴더 다중 선택 완성(C3/C4)** + 러버밴드 드래그 선택 + 혼합 파일 작업.
  3. **경로 바(브레드크럼)** · **탭 모델**(현재 탭은 placeholder) · **설정 JSON 영속화**(F15 준비됨) · 컬럼 설정 모달([23](23-column-system.md)).
  4. **설정 화면(UI)** — 단축키 편집(다중 바인딩·키보드+마우스, [26 §2-1·§8](26-command-palette.md)) · 표시 옵션(F24) · **창 위치 복원 on/off**([28](28-window-session-restore.md)). (설계됨·구현 대기)
  5. **창 위치/크기 복원 + 다중 모니터 보정**(Primary LEFT/TOP) — 설계 [28](28-window-session-restore.md), 설정 시스템 β와 함께 구현.
  - 구조(트랙 B 진행 중): MainWindow.xaml.cs **955줄**(계속 축소 대상) — **`Nexa.ViewModels`(net8.0) 분리 착수**(순수 로직 맥 빌드/테스트, B-1) + `bool left` 이중화를 **`PanelView` 객체로 소거 완료**(B-2a). 남은 순수 로직 이관·`PanelControl` XAML dedup(B-3) 지속 — [11](11-dev-environment.md) §6-1.

## 7. 다른 PC에서 시작 / 컨텍스트 공유

- clone 후 **`CLAUDE.md`(자동 로드) + 이 STATUS** 로 즉시 인계 → [14](14-context-sharing.md).
- **저장소 가시성**: 현재 **private**, 어느 정도 진행 후 **public 전환 예정**(DR-5 소스공개 방향). 공개 대비 지금부터 비밀 커밋 금지 규율 적용.
- 비밀(서명 인증서·자격증명·라이선스 키·사업/법무)은 **저장소 밖**(GitHub Secrets·비밀번호 관리자·
  private repo·암호화 커밋). `.gitignore`로 사고 예방.

## 8. 문서 인덱스

**[TODO.md](TODO.md)**(할 일 백로그·범위 산정, living) · **[BUGS.md](BUGS.md)**(알려진 이슈) · CLAUDE.md(이식 메모리) · 00 비전 · 01 아키텍처 · 02 로드맵 · 03 기능 · 04 트렌드백로그 ·
05 요구사항 · 06 ADR-0001(스택) · 07 플래그십 · 08 경쟁조사 · 09 플러그인 · 10 결정기록 ·
11 개발환경 · 12 포터블 · 13 라이선스 · 14 컨텍스트공유 · 15 개발방법론 · 16 프로젝트구조 ·
17 라이선스인증 · 18 빌드&테스트 · 19 구현기능현황 · 20 UI레이아웃 · 21 ADR-0002(파일뷰) · 22 ADR-0003(뷰/패널모듈) · 23 컬럼시스템 · 24 검색 · 25 일괄리네임 · 26 커맨드팔레트 · 27 경로바컴포넌트 · 28 창위치/세션복원 · 29 ADR-0004(코어트리모델) · 30 용어집(인터롭/배관) · 31 스크롤위치노트 · journal/ (작업기록)
