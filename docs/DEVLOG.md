# DEVLOG — 개발 진행 기록 (시간 역순)

> **전체 개발 진행을 시간순(최신이 위)** 으로 관찰하는 단일 기록. 세부 커밋은 [BRANCHES.md](BRANCHES.md)·git 로그.
> 짝 문서: 목적·기능·마일스톤 관점 = **[MILESTONES.md](MILESTONES.md)**.
>
> **기록 규약(2026-07-05~)**: 진행 기록은 **일자 단위**로만 만든다.
> - 하루의 상세는 `journal/YYYY-MM-DD.md`(파일 **내부는 시간 역순**, 최신이 위)에 적는다.
> - 이 DEVLOG에는 그날의 **요약 섹션을 맨 위에 추가**(역순 유지)하고 해당 일자 파일로 링크한다.
> - 과거의 세션별/시각별(`YYYYMMDD_HHMMSS_*`) 저널과 라운드 워크로그(`refactor-00N-worklog`·`bottom-panel-worklog`)는
>   **통폐합 이전 아카이브**로 남긴다(상세 근거용). 신규 기록은 위 규약을 따른다.

---

## 2026-07-11

- 🏷️ **릴리스 `0.3.6`**: 경로바 자동완성(PATH-SUG 1~3차, PR#22~24) — 자산=setup.exe 단독(포터블 비활성 유지). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **제안 목록 폰트 과대 표시 수정(PATH-SUG 3차)**: `ListViewItem` 기본 스타일의 `FontSize=14` 명시가 주입값을 덮음 → `ContainerContentChanging`에서 컨테이너 로컬 값 주입(+미주입=경로바 폰트 폴백). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **경로바 자동완성 다듬기(PATH-SUG 2차)**: ↑/↓ 순환(PreviewKeyDown — TextBox 방향키 내부 소비 우회)·첫 항목에서 ↑=**조회 시점 입력 복원**·제안 목록=파일 목록 폰트. 트랩: WinUI TextChanged **비동기·병합** → 불리언 억제 대신 기대 텍스트 마커(실측 검증). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **경로바 편집 자동완성(PATH-SUG, `feat/pathbar-suggestions`)**: 탐색기식 폴더 제안 드롭다운 — 순수 `PathSuggestions`(xUnit +7, 총 88)+`SuggestionProvider` 주입(컨트롤 IO 비종속), ↑/↓ 미리 채움·Enter/클릭 이동·ESC 단계 닫기, 환경변수 입력 지원. UIA 스크린샷 검증. 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- 🏷️ **릴리스 `0.3.5`**: NAV-CLICK+NAV-UPFOCUS 수정 포함 · **포터블 zip CI 빌드 잠정 비활성**(사용자 — 방향성 재정리, 자산=setup.exe만) · **PKG-6 등록**(32/64비트 별도 빌드 검토). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **상위 이동 선택 소실 수정(NAV-UPFOCUS, `fix/watcher-preserve-selection`→main PR#21)**: watcher 자동 갱신(ReloadPanel)이 핸들 교체+ScrollToTop으로 선택·캐럿·스크롤을 파괴(활동 잦은 폴더에서 GoUp 직후 매번) → **보존→복원**으로 무간섭화. UIA+watcher 유발 실검증. 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- 🔴 **네비 버튼 클릭 무동작 진범 확정·수정**(`fix/navbtn-click-focus-steal`): 사용자 결정타 제보("호버는 되는데 클릭만 안 됨") — PR#12의 패널 press 시 그리드 포커스 회수가 **버튼 press 중 ButtonBase pressed를 리셋**해 Click 미발화. `IsWithinButton` 제외로 수정, 합성 실클릭 before/after 대조 검증(0회→발화). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- 🏷️ **릴리스 `0.3.4`**: 네비 버튼 상태 정합(PR#18 동기+PR#19 시각 식별) — zip+setup 자동 첨부, 설치본 갱신. 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **네비 버튼 상태 식별 디자인**(`fix/navbtn-hover-affordance`→main PR#19): 사용자 재리포트(설치본 0.3.3=상태 동기 미포함에서 no-op 버튼이 활성처럼 보임) → NavBtnStyle 템플릿화 — hover 하이라이트·pressed 강조색·**disabled 글리프 0.3 흐림**. PR#18과 함께 0.3.4 릴리스. 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **네비 버튼 무동작 리포트 진상 + 초기 상태 수정**(`fix/navbtn-initial-state`): 주범=버려진 출력 트리의 07-06 고아 exe(전체 clean 해소) · 실버그=**세션 복원 시 UpdateNavButtons 미호출**(빈 히스토리에 Back/Fwd 활성 오표시)→복원 직후 명시 호출. UIA 자동화로 검증 + **0.3.3 릴리스 자산 실검증(정상)**. 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- 🏷️ **릴리스 `0.3.3`**: BUG-010 수정판 — 0.3.2 자산 대체(0.3.2는 결함 경고·prerelease 강등). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- 🔴 **BUG-010 — 0.3.2 배포 자산 결함 2건 해결**(`fix/portable-render-xbf`→main PR#17): ①렌더링 깨짐(loose 소스 `.xaml` 폴백이 x:Bind를 죽임)→**컴파일 `.xbf` 게시 루트 배치** ②시작 크래시 0xC0000374(**PageHeap으로 범인 특정=WinAppSDK 1.6 MRT Core `GetDefaultPriFile`** 힙 손상, self-contained 한정)→**WinAppSDK 1.8 업그레이드**. PageHeap 3/3·일반 5/5·스크린샷 검증. 교훈: 배포 자산은 산출물 자체로 실행 QA. 상세 [BUGS.md](BUGS.md)·[journal/2026-07-11.md](journal/2026-07-11.md).
- 🏷️ **릴리스 `0.3.2`**: setup.exe 동봉 — 바이너리 자산 2종(포터블 zip+설치기, CI 자동 첨부). 상세 [journal/2026-07-11.md](journal/2026-07-11.md).
- **클래식 설치기 setup.exe(PKG-5, `feat/packaging-setup`→main PR#16, dispatch 실검증)**: MSIX 서명 조사(Azure Artifact Signing=한국 개인 불가 → Store 제출 또는 OV 클라우드 서명이 현실 경로, [TODO PKG-4](TODO.md)) 후 서명 확정 전 설치형 채널로 **Inno Setup** 채택 — `nexa-setup.iss`(사용자 단위 기본·언인스톨러·AppId 고정)+`make-setup.ps1`(설치형 self-contained 게시). CI `package` job이 zip+setup.exe를 빌드해 태그 릴리스에 **모두 자동 첨부**(Inno=러너 기본 포함, 로컬 빌드 불요). 상세 [journal/2026-07-11.md](journal/2026-07-11.md)·[docs/12 §7](12-packaging-portable.md).

## 2026-07-10

- 🏷️ **릴리스 `0.3.1`**: CI `package` 잡에 Release 자동 첨부 단계 추가 후 태그 — **첫 바이너리 자산** `NexaDir-0.3.1-portable-win-x64.zip`(CI 빌드) 릴리스 동봉. 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **패키징 1차 — 포터블 폴더 zip(PKG-1~3, `feat/packaging-portable`→main PR#15, CI green·package 잡 아티팩트 실검증)**: `AppPaths` 경로 단일 원천(+`portable.ini`/`--portable` → 영속물 전부 `exe\data\`) · `make-portable.ps1`(self-contained 게시=런타임 번들·검증·마커·zip ≈64MB) · CI `package` job(태그·수동). 게시 특수 대응 3건 실측 해소(RID .pri·NETSDK1152·**self-contained 라이브러리 XAML 루트 URI 미해석 → loose 사본 폴백**). zip 실행·`data\` 영속·%APPDATA% 미오염 자체검증. MSIX(PKG-4)=인증서 결정 대기. 상세 [journal/2026-07-10.md](journal/2026-07-10.md)·[docs/12 §7](12-packaging-portable.md).
- 🏷️ **릴리스 `0.3.0`**: `0.2.0`(07-06) 이후 M1 후반 진행분(PR#4~14 — 설정 시스템·글꼴/밀도·i18n 외부 언어팩·도구 모음·패널/터미널 UX·타입어헤드 완성·4차 감사·PREF-9 재시작)을 묶어 태그·GitHub Release. 버전 동기(Cargo·csproj·lang `@app`). 패키징 릴리스는 `0.4.0`으로 이월. 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **재시작 필요 설정 확인창+자체 재시작(PREF-9, `feat/pref-9-restart`→main PR#14, CI green)**: 언어 변경 시 **실효 언어 변화 판정**(원복·등가 전환은 안 물음) → 확인창(지금 재시작/나중에) → 승인 시 **선-flush 후 `AppInstance.Restart`**(미패키지 폴백=exe 재기동+Exit). 항목별 판정 위임으로 일반화(후속: 테마팩 등). 메커니즘 문서 [docs/40 §9](40-preferences-system.md). 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **4차 감사(`refactor/004-audit`, 4커밋)**: 병렬 분석 4종(C# 스멜·Rust·메모리·속도) → Rust 정렬/타입어헤드 핫패스 무할당화+FFI 선택 id 가드, C# 중복 3종 추출·타이머/구독 수명 정리, `VtScreen.Lines` 실체화 제거·`Count` P/Invoke 캐시·**실체화 캐시 이빅션**(대형 폴더 수십 MB 증식 해소)·**DATAS 활성**(상주 NFR 1차)·settings.json 이중 로드 제거. 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **앱 아이콘 신규 디자인(main `9150329`)**: 기존 트리/행 컨셉 → **풀블리드 폴더 + 대형 초록 `>_`**(마테리얼 플랫, 앞판=터미널 화면처럼 다크, accent 파랑 실루엣 테두리 — 흰/검정 배경 모두 대비 검증). `nexa-dir`(다크판, 기본)+`nexa-dir-light`(예비) 2세트, 산출은 1024 마스터+ICO만(실사용 없는 16~512 PNG 제거). 생성기 팔레트 주입식 개편.
- **타입어헤드 마감 + UX 버그 일괄(`feat/typeahead-hud`→main PR#12, 18커밋)**: **타입어헤드 완성**(docs/32 1~5 전부 ✅) — 문자 트리거를 검증된 **전역 KeyDown 합성**으로(CharacterReceived는 포커스 전용·비버블로 확정), 파일명 허용 문자 전체+Space 조건부(접두사 진행 중), **검색어 HUD**(`EphemeralOverlay` 신규 — 위치 3×3 매트릭스 피커[hover 50%·선택 100%]·입력 옵션 3종[특수문자/Space/Backspace] 설정·영속) · **터미널 포커스 강탈 근본 수정**(선택 변경→도크 재렌더→Start가 매번 포커스 훔침 → 재렌더 무포커스, 파일 클릭 시 그리드 포커스 회수) · **크기 표기**(컬럼=KB/MB/GB 적응, 정보 패널=Bytes 단복수) · 보조 창(설정·전송) 앱 아이콘. 상세 [journal/2026-07-10.md](journal/2026-07-10.md)·[docs/32](32-typeahead-find.md).
- **패널·터미널 UX(`feat/panel-terminal-ux`→main PR#11, 10커밋)**: **하단 도크 대원칙(docs/20)** — 듀얼=좌↔좌·우↔우 / **싱글=활성 패널 탭 기준**(정보·미리보기·터미널), 전환 시 자동 재연결 · **분할 위치 저장·복원**(상단 패널·하단 도크 — 숨김 직전 비율 저장, 기본 50:50) · **"터미널에서 열기(내장)"** 컨텍스트 메뉴(폴더 1개 선택) · **터미널**: 선택 드래그 자동 스크롤·우클릭=선택 복사·긴 출력 줄바꿈 없음(설정 "터미널" 카테고리 — 줄바꿈↔최대 길이[기본 240] 선택)·가로 스크롤바 입력줄 가림/버블링 수정 · **메뉴 바**: 다크 테마 대응(VS Code식 토큰)+2단 서브메뉴 지원 — 테마 3항목 "테마▸" 그룹, **기본 테마 System** · 우 패널 재표시 비율 복원. 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **도구 모음 슬라이스 2 + 플랫 메뉴 + 복사·터미널 편의(`feat/toolbar-groups`→main PR#10, 12커밋)**: **파일 표시 그룹**(숨김=RedEye/Hide 아이콘 교체·도트파일=⋯ 토글, 세로 구분선, 표시 메뉴·빈영역 메뉴와 설정 공유) · **설정 "도구 모음" 카테고리**(그룹/항목 ▲▼ 순서, `Toolbar` 섹션 영속) · **플랫 컨텍스트 메뉴**(사각 프레젠터 — 빈영역[섹션+표시 토글 ✓+단축키 컬럼]·탭[3섹션+Ctrl+W]) · **메뉴 바 체크 칸 상시 예약**(텍스트 정렬 통일) · **터미널 위치 이동**(도구 모음 밀착 정사각 + 하단 도크 한 몸 스타일·터미널 활성 시 초록, 활성 탭 폴더로 cd·pending 전송) · **Copy as name/path**(파일·탭 공용, path 위에 name — 동사 대체 동반 삽입 `VerbReplacement.Before`) · **컨텍스트 메뉴 글꼴 슬롯** · 탭 전환 시 도크 폴더 미갱신 버그 수정. [docs/44 §5](44-toolbar-and-launcher.md). 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **글꼴 설정 + 밀도 + 설정 창 VS Code식 재구성 + i18n 전면 적용(`feat/font-settings`→main PR#9, 14커밋)**: **글꼴 슬롯 5종**(기본[메뉴·탭·경로·하단 정보]/콘솔[쉼표 폴백·셀 폭 실측]/상태표시줄/파일 목록/파일 헤더[꾸미기만]+폴더 굵게 토글) — `FontOptions` 영속+`ApplyFonts` 라이브 적용, App.xaml 암시적 TextBlock 스타일 제거(상속 차단 해소), **탭 높이=기본 글꼴 크기 연동**. **밀도** — 행 상하 1px·행간 0(-1px 겹침으로 선택 박스 4면+경계 1px)·탭 20px(Double Commander 지향). **설정 창** — 좌측 검색+카테고리 트리+설정 레지스트리(그룹/항목 이중 필터), **영속 설정 전부 UI 수록**(+`Sort.FoldersFirst` 영속 신규·dwell 라이브 재반영), 글꼴 입력=목록+직접 입력(엔터/포커스 이탈 검증·경고, 편집 콤보 초깃값 버그 수정). **i18n 전면 적용** — 신규 키 ~140종(ko/en/임베디드 3벌)으로 상태바·메뉴·다이얼로그·전송창·캡션·정보창까지 전부(잔여=개발자 진단뿐). 경로 바 편집 모드 높이/글꼴 일치. QA 5라운드 반영. [docs/40 §8](40-preferences-system.md). 상세 [journal/2026-07-10.md](journal/2026-07-10.md).
- **도구 모음 정리(docs/44 후속)**: 구형 자리표시자 버튼 8종(뒤로/앞으로/위로/새로고침·아이콘/목록/컬럼/갤러리)+구분선 제거(전부 XAML 전용 — 이벤트 없음, 경로 바 네비 버튼은 별개 유지) → 도구 바 = `InitToolbars` 내장 도구 3종만. **높이 최소화**(패딩 6,2 + 버튼 `MinHeight=0`, 기본 32 해제). 상세 [journal/2026-07-10.md](journal/2026-07-10.md).

## 2026-07-09

- **도구 모음(내장) + 퀵 런처(외부) 슬라이스 1(`feat/toolbar-launcher`→main)**: 16×16 아이콘 빠른 실행 도구 2종 체계 — **도구 모음**(개발자 제공: 현재 폴더 터미널[wt→pwsh→cmd 폴백·Ctrl+Shift+T]·파일 찾기[M3 스텁]·이름 바꾸기[F2]) + **퀵 런처**(사용자 등록 외부 프로그램, 시드=VS Code 활성 탭 폴더 열기·**exe 아이콘 16px**). `ToolLauncher`(실행·VS Code 탐지·실패 격리) + `InitToolbars`. CRUD·영속·단축키 배정은 후속. 설계 [docs/44](44-toolbar-and-launcher.md). 상세 [journal/2026-07-09.md](journal/2026-07-09.md).

## 2026-07-08

- **경로 복사(Copy as path) — 셸 동사 제자리 대체(`feat/copy-as-path`→main PR#5, CI green)**: 교차폴더 다중선택에서 셸 "Copy as path"가 한 폴더만 복사하던 문제(셸 `IContextMenu`=단일 부모 폴더 한계) 해결. `ShellContextMenu.VerbReplacement`로 HMENU의 `copyaspath` verb를 찾아 **삭제+삽입(제자리 대체)**, `CopyPathsAsText(Targets)`가 축소 이전 전체 선택을 클립보드로. 기본=따옴표+역슬래시·**Alt=POSIX(`/`)**. [docs/38 §7-5](38-adr-0005-shell-context-menu.md). + **외부 파일 위치 맵 [docs/43](43-external-files-and-config.md)** 신설(중복은 34/40/42로 단일화). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **i18n 외부 언어 파일(`.lang`)(`feat/i18n-lang-files`→main PR#4, CI green)**: 임베디드 문자열 → **설치 `lang\` + 사용자 `%APPDATA%\NexaDir\lang\`** 외부 파일화(재빌드 없이 언어 추가/수정). 포맷 심(`ILangFormat` JSON↔properties, `LangFormats.Active` 한 줄 전환)·`LangCatalog`(병합)·임베디드 en 안전망·설정 언어 페이지 동적화. 기준 언어=영어. **xUnit +7(총 81)**. [docs/42](42-i18n-language-files.md). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **설정(Preferences) PREF-1 + i18n 인프라 + 레이아웃 영속(`feat/preferences`→main)**: `SettingsStore`(`settings.json` 로밍·영속 규율 공용) + `Ctrl+,` 설정 창(모양/레이아웃/메뉴/언어) — **재시작 소실 옵션 4벌 영속**. 순수 `Localizer`(폴백 체인, xUnit)+`LocExtension` 마크업 확장+메뉴 바 이관. 퀵 런처·우 패널 토글 `session.json` 영속. [docs/40](40-preferences-system.md). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **컨텍스트 메뉴 사용자화 + Checksum(`feat/context-menu-custom`→main, QA 통과)**: 커스텀 항목을 **선언적 레지스트리**(Children 서브메뉴 지원)로 재설계 + 설정 스키마(표시/순서/섹션 위치 — 설정 UI 후속, [docs/38 §7](38-adr-0005-shell-context-menu.md)). **Checksum ▶ MD5/SHA-1/256/384/512/CRC32** — 파일별 줄·폴더 제외·복사 대화상자. 다중 파일 1줄 표시 버그(AcceptsReturn 순서) 수정. 인코딩(Base64)은 구현 후 사용자 결정으로 제거. 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **터미널 위치 정확도 라운드(5커밋) + 선택·복사·붙여넣기 — 실기 QA 통과**: ①세션 시작을 첫 레이아웃 이후로(초기 20×5 격자 어긋남) ②SU/SD·IND/NEL 스크롤 시퀀스 ③DECSTBM 스크롤 마진 ④**근본 원인 = UseLayoutRounding 누적 드리프트**(행당 0.45px → "1.8칸") → 줄 Canvas 절대 배치 ⑤Left/Top 정렬 고정. + **마우스 드래그 선택·Ctrl+C(선택 시 복사/아니면 SIGINT)·Ctrl+Shift+C·Ctrl+V**. 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **테마 시스템 S1(`feat/theme-system`→main)**: 영역 구분용 임시 틴트 전면 제거 → **시맨틱 토큰 10종**(App.xaml ThemeDictionaries)·**라이트 팔레트 정비**·구성(O) 메뉴 **테마 시스템/라이트/다크**(기본 Light — DR-2 다크 기본은 다크 정비 후 재결정), 후속 세부 설정 UI(테마팩·폰트·밀도) 설계 = [docs/39](39-theme-system.md). + **패널 보기 토글을 표시(S) 메뉴로 이관**(상태바=상태 전용) + **파일 목록 가로 스크롤**(넘칠 때만·헤더 동기). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **앱 아이콘 제작·적용**: GDI+ 생성기 `scripts/make-app-icon.ps1`(재현 가능) — 다크 라운드 + 폴더 루트 트리 + 다중 선택 accent 행 + `</>`(개발자 지향) 컨셉. PNG 9종(16~1024)+멀티사이즈 ICO, exe 임베드(ApplicationIcon)+창 SetIcon. 파일명 `nexa-dir-*`(공용 브랜드와 구분). 저널 **⏱ 시각 표기 규약** 도입(git 커밋 시각 기준, CLAUDE.md §6). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **breadcrumb 긴 경로 개선**: 폭 초과 시 끝(최근 폴더)으로 스크롤 유지(오른쪽 정렬 효과) — 경로 변경·리사이즈 공통. 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **탭 UX 일괄(TAB-DND·TAB-MENU, 9커밋)**: **탭 드래그 재정렬/패널 간 이동·Ctrl 복제**(탭 영역 한정·XAML/OLE 공용 계획·**삽입 위치 하이라이트·바 빈 영역=맨 끝**) · **탭 우클릭 메뉴**(새 탭/닫기/모두 닫기[잠금 보존]/잠금·고정 — 우클릭 시 탭 활성화) · **잠금=열쇠(오른쪽 끝)·고정=핀(이름 앞)+핀 그룹 정렬·세션 영속** · 긴 제목 말줄임(…) 복원 · **경로·항목 수 헤더 토글**(기본 감춤, 표시 메뉴). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **러버밴드(마퀴) 다중 선택(B-4 부분)**: 미선택 행/빈 공간 드래그=밴드 선택(선택 행=DnD 유지), 행 히트영역=컬럼 총폭(크기 뒤=배경), 4px 임계·가상화 안전 인덱스 범위·자동 스크롤. 상세 [journal/2026-07-08.md](journal/2026-07-08.md).
- **B-13u Undo/Redo 마감 — 실기 QA 통과**(삭제 복원·복사·이동·새 폴더 전부 Ctrl+Z/Y 확인): **셸 메뉴 "삭제" 가로채기**(`verbInterceptor` — GCS_VERBW canonical verb 조회, "delete"→`DeletePaths` 라우팅으로 undo 기록 통합; 셸 직접 수행이라 미기록이던 버그 수정) + **빈 영역 메뉴에 실행 취소/다시 실행 항목**(마지막 작업 설명·Ctrl+Z/Y 표기·비활성 처리). 상세 [journal/2026-07-08.md](journal/2026-07-08.md).

## 2026-07-07

- **B-13u Undo/Redo S1+S2 — 탐색기식 Ctrl+Z/Y 완성**: `Nexa.ViewModels.OperationHistory`(스택 2개·redo 무효화·실패=소실+알림) + `MoveBatchOp`/`CopyBatchOp`/`RenameOp`/`CreateOp`(삭제 주입으로 Windows API 격리, **xUnit 10건·총 67 green**) + **S2 `DeleteBatchOp`/`RecycleBin.cs`**(휴지통 셸 폴더 열거→원래 경로 매칭→`undelete` 동사 — 삭제 복원). 기록 배선 = `TransferPathsInto` 실수행 쌍(배치=1 트랜잭션)·이름변경·새로만들기 3종·휴지통 삭제. Ctrl+Z/Ctrl+Y(+Ctrl+Shift+Z), 상태바 표기, 완료 후 재로드. 잔여=nexa-ops 이관·다중 삭제본 시각 비교. 상세 [journal/2026-07-07.md](journal/2026-07-07.md)·[docs/33 §B-13u](33-file-ops-dnd-design.md).
- **B-2 셸 컨텍스트 메뉴 착수 — [ADR-0005](38-adr-0005-shell-context-menu.md) Accepted + S1 구현**: 행 우클릭 = **클래식 네이티브 셸 메뉴(`IContextMenu` HMENU) + 고유 항목 병합**(ID 대역 분리 셸 1~0x7FFF/고유 0x8000+ — 폴더에 붙여넣기·이름 바꾸기 F2·완전 삭제). `ShellContextMenu.cs` 신규(수동 COM 인터롭, IContextMenu2/3 메시지 포워딩="보내기"/"열기 방법", Shift=확장 동사, 셸 확장 예외 격리). 셸 명령 후 800ms 지연 재로드(watcher 보완). S2(빈영역 배경 메뉴)·S3(폴리시) 남음. 상세 [journal/2026-07-07.md](journal/2026-07-07.md).
- **☑ [BUG-009](BUGS.md) 해결 — 상승(관리자) 프로세스 OLE 드롭 폴백**: 원인 확정 — UAC OFF PC는 탐색기 실행도 전부 상승 + **WinUI 3가 상승 프로세스의 인바운드 드래그를 플랫폼에서 거부**(microsoft-ui-xaml#7690/#10119; 어제의 UIPI 배제 판단은 무효). 해결 — 상승 감지 시 XAML 브리지 HWND에 **고전 OLE `IDropTarget` 폴백**(신규 `OleDropTarget.cs`) 등록: CF_HDROP 추출·히트테스트(폴더 행/패널 현재 폴더)·기존 판정 규칙·전송 엔진 합류, 최적화 이동(원본 중복 삭제 방지). 비상승은 XAML 경로 유지. **탐색기→앱 복사 실기 확인**, 진단 코드 제거. 상세 [journal/2026-07-07.md](journal/2026-07-07.md).
- **🔴 [BUG-009](BUGS.md) 등록(긴급, [TODO B-16dnd](TODO.md))**: 외부(탐색기→앱) 드래그가 여전히 금지 커서 — UIPI 배제(일반 권한 재현), unpackaged WinUI 3의 StorageItems 미노출/DragOver 미도달 의심. **진단 로그 심음**(`%TEMP%\nexa-dnd-debug.log`) → 다음 세션 로그로 원인 분기. UIPI 함정(관리자 실행 시 DnD 차단)은 docs/33에 기록. (→ 같은 날 위 항목에서 해결)
- **Drag & Drop 전면 검토 → P1/P2/P3 개선**(3커밋): **외부(탐색기→앱) 파일 드롭 수신**(DND-EXT — 행·탭·빈 영역, deferral+StorageItems→기존 전송 엔진 합류, 복사 기본/Shift=이동; 기존 "수락 표시 후 무동작" 버그 해소) · **자기/하위 폴더 드롭 UI 차단**(DND-CYCLE) · **드래그 시작 StorageItem 병렬 취득**(대량 선택 지연 완화) · **외부 이동 드롭 후 패널 갱신** · 캡션 변경 시만 설정·문구 정리. 상세 [docs/33](33-file-ops-dnd-design.md) 07-07 절 · [journal/2026-07-07.md](journal/2026-07-07.md).
- **내장 터미널 UX 일괄 개선**(BP-T 후속): **캐럿 표시**([BUG-007](BUGS.md) ☑ — 오버레이 블록·포커스 시 깜빡임/비포커스 중공) · **클릭 포커스 안정화**(handledEventsToo+enqueue 재포커스) · **Tab 자동완성**(포커스 이동 차단·Shift+Tab 역방향) · **Backspace=DEL(0x7F)**(0x08=단어삭제 오매핑 수정) · **faint(SGR 2) 렌더**([BUG-008](BUGS.md) ◐ — PSReadLine 예측을 VS Code처럼 연한 회색으로) · **고정폭 셀 렌더**(Canvas 절대 배치 + 전각 2칸 — 캐럿/열 드리프트 해소) · **ECH(CSI X)·DECSC/DECRC** 구현(백스페이스 잔상 제거). 빌드 경고 0·에러 0. 상세 [journal/2026-07-07.md](journal/2026-07-07.md).

## 2026-07-06

- **🏷️ 릴리스 `0.2.0`** — `0.1.0` 이후 M1 대규모 진행분(파일 조작 전체·컬럼 정렬·타입어헤드·하단 패널[정보·미리보기·ConPTY 터미널]·미리보기 플러그인 SDK·문서 위키)을 묶어 태그·GitHub Release. 버전 동기화(Cargo/csproj 0.2.0). 아티팩트(MSIX/포터블)는 패키징 인프라 미비로 후속([12](12-packaging-portable.md)). 상세 [journal/2026-07-06.md](journal/2026-07-06.md).
- **전체 문서 통합 최신화**(다른 PC 병합분 정합): 이 PC 저장소 최신화(main FF 98커밋)·앱 빌드 green 후, 지연된 기준·참조 문서를 맞춤 — `CLAUDE.md`(현단계·구조·DR-6·다음단계)·`STATUS`(07-06 하단패널 블록)·`MILESTONES`(BP-2/터미널/플러그인 ☐→✅·M2 🚧)·`docs/16`·`docs/19`(카운트 34/57·ABI v7). 상세 [journal/2026-07-06.md](journal/2026-07-06.md).
- **하단 패널 콘텐츠 `feat/bottom-panel-info` → main 병합**(`3dd423a`): BP-2(정보·미리보기·플러그인 SDK) + BP-T(터미널).
  - **미리보기 시스템 + 플러그인**: 표준 `IPreviewProvider`+레지스트리(텍스트/이미지) · **퍼미시브 MIT SDK `Nexa.Plugins`**(DR-6)+샘플+**개발 매뉴얼**([36](36-plugin-development.md)) · 크기 상호연동 · 로딩 부하 방지 wrapper(디바운스/취소).
  - **임베디드 터미널**: **ConPTY** + **VT 에뮬레이터**(`VtScreen`: 색·화면버퍼·SGR·스크롤백, [37](37-terminal.md)) · lazy 로딩 · exit 재시작 · 작업경로=활성 탭 폴더(홈 폴백) · 키보드 캡처(전역 단축키 개입 차단). 알려진 이슈 [BUG-007/008](BUGS.md)(캐럿·색 → BP-T3).
  - 병합 후 `feat/bottom-panel-info` 로컬+원격 삭제. [BRANCHES](BRANCHES.md) 시간 역순으로 재정렬.
- 상세: [journal/2026-07-05.md](journal/2026-07-05.md)(BP-2/BP-T 세션) · [journal/2026-07-06.md](journal/2026-07-06.md).

## 2026-07-05

- **문서 통폐합**: 시간순 기록을 이 **DEVLOG**로, 기능·마일스톤 기록을 **MILESTONES**로 통합. 일자 단위·시간 역순 규약 도입. 브랜치 이력은 [BRANCHES.md](BRANCHES.md).
- **브랜치 정리**: 병합 완료 브랜치(refactor/001~003-audit·feat/bottom-panel) 로컬+원격 삭제. 이력은 BRANCHES.md/워크로그에 보존.
- **하단 패널 BP-1**(`feat/bottom-panel` → main `01c85a7`): 하단 도킹을 실제 콘텐츠 호스트로(`BottomDockView` — 정보/미리보기/Hex/터미널 종류 선택·스왑, 정보=현재 폴더) · **Ctrl+`** 표시/숨김 토글 · 하단 패널 상태(표시/높이/분리/종류) **session.json 저장·복원**. 미리보기/Hex/터미널(ConPTY)은 후속.
- **refactor/003-audit → main 병합**(`bd45f86`).
- **파일 전송 단일 엔진 통일**: DnD·붙여넣기(Ctrl+V·컨텍스트)가 `TransferPathsInto` 하나로 — **덮어쓰기 확인**(예/모두 예/건너뛰기/취소)·**바이트 진행률**·**진행 창**(맨앞·취소·자동닫기 off) · 확인 프롬프트를 진행 창 안에 embed(ContentDialog XamlRoot 오류 해결) · **OS 클립보드 붙여넣기**(탐색기 복사 인식) · 외부 DnD **StorageItems**(대상이 파일 열기)/Alt=경로.
- **탭 세션 저장/복원**(`session.json`, 요청/수행 분리·단일 Tick 코얼레싱) · **새로 만들기**(폴더/파일/바로가기) · **수정 날짜/시간 컬럼**(DateTime) · **경로 바 환경변수 해석**(%VAR%·$env:) · cargo fmt(CI 게이트) · 이 PC 식별 문서.
- 상세: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md)(세션 요약 2026-07-05) · [journal/archive/bottom-panel-worklog.md](journal/archive/bottom-panel-worklog.md).

## 2026-07-04

- **파일 조작 UX 심화 배치**(B-7~B-15h): 더블클릭 실행 · FileOps(맥 테스트) · 우클릭 컨텍스트 메뉴(폴더/파일/빈영역) · 복사/잘라/붙여/삭제 단축키 · DnD(폴더 이동+자동스크롤·좌우 패널·탭 hover·ESC 취소) · **디스크별 DnD**(같은=이동/다른=복사, Ctrl/Shift 강제) · 폴더 hover 진입 · **watcher 1차**(자동 갱신) · 확장자 컬럼. 버그(BUG-002~006).
- **DnD 탐색기 파리티**: 라이브 캡션("…에 복사/이동")·자기 폴더 드롭 규칙. 관리형 한계(DND-KEY/FONT/STACK)는 셸/OLE 트랙으로 보류.
- **헤더 정렬 COL-2/3**: 코어 비교자(SortKey/SortSpec)·ABI v6·UI(3상태 순환·▲▼·원문자 순번)·다중열(Shift)·좌우 패널 독립.
- **타입어헤드 설계 확정**([docs/32](32-typeahead-find.md)): A/B/C 범위·EphemeralOverlay.
- 상세: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md)(세션 요약 2026-07-04).

## 2026-07-03

- **refactor/003-audit 착수**: 4축 통합 감사(설계·성능·구조·FR/NFR) → 트랙 A~E 백로그. **B-6 인라인 이름 변경**(선택 후 재클릭/F2).
- **타입어헤드 TA-1/2/4·5**: 코어 `find_prefix`(ABI v7)·`TypeAheadBuffer`·앱 배선.
- **refactor/002-audit → main 병합**(`1d9d312`).
- 상세: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md) · [journal/archive/refactor-002-worklog.md](journal/archive/refactor-002-worklog.md).

## 2026-07-02

- **refactor/001-audit**: 1차 통합 감사 + **C1 코어 트리/선택 이관**(`nexa-tree` 가시행 스트림·C ABI·호스트 안전계층·앱 가상화 재배선·10만 노드 벤치·핸들 캐시) → **main 병합**(`b38e6b3`, 태그 `0.1.0`).
- **refactor/002-audit 착수**: 트랙 A 성능(백그라운드 열거·범위 diff·아이콘 LRU 캐시·경로→NodeId ABI v5) + 트랙 B 구조(PanelView·`Nexa.ViewModels`+C# xUnit).
- 상세: [journal/archive/refactor-001-worklog.md](journal/archive/refactor-001-worklog.md) · [journal/archive/refactor-002-worklog.md](journal/archive/refactor-002-worklog.md).

## 2026-07-01

- **레이아웃 정교화·플래그십 초안**(F7~F19): 좌/우 듀얼 목록·하단 도킹 연동 · `NexaFileGrid` 추출 · 스플리터 자석 스냅 · **인라인 폴더 펼침 + Finder식 컬럼** · 파일 선택(단일/Ctrl/Shift) · 키보드 이동·펼침·캐럿 · **패널별 탭**·닫기 · 네비(뒤로/앞으로/위로·Alt) · 계층 경로 바 `NexaPathBar` · 숨김/점 파일 토글.
- 빌드 이슈 해결(Sizers TFM 22621 정합).

## 2026-06-30

- **킥오프**: 비전·요구 확장·설계·ADR(DR-1 WinUI3+Rust 코어·DR-5 라이선스)·플러그인/터미널/런처 설계·라이선스 인증 설계·컨텍스트 공유·개발 방법론.
- **스캐폴딩 + 환경 검증**: `core/`·`app/Nexa.App`·CI(mac/win)·LICENSE·bootstrap. Windows 풀빌드 실측.
- **M0 데이터 흐름 수직 슬라이스**: 인터롭 PoC → nexa-vfs 스트리밍 열거 → 인터롭 디렉터리 열거 → C# 바인딩+UI → ItemsRepeater 가상화 → 레이아웃 골격(7행 그리드). `0.1.0` 태그 기준.
- 상세: [journal/](journal/) 2026-06-30 세션들.
