# BRANCHES — 브랜치 기록 (Branch History, 시간 역순)

> **목적**: 병합 후 삭제되는 작업 브랜치의 이력을 남긴다. 각 브랜치가 **언제 생성**되고 **무슨 작업**을 했으며
> **어떤 커밋**이 있었고 **언제 main에 병합**되고 **언제 삭제**되었는지 추적한다.
> **규약**: 브랜치는 main 병합·빌드 green 확인 후 **로컬+원격 삭제**하고, 이력은 이 문서 + 각 브랜치 워크로그(journal)에 보존.
> **정렬**: **시간 역순(최신이 위)**. 새 브랜치를 만들면 표·상세 모두 **맨 위에 추가**한다. 시각=커밋 committer date(KST).

## 요약 (시간 역순)

| 브랜치 | 생성 | 병합(커밋) | 삭제 | 커밋수 | 작업 요약 | 상세 |
| --- | --- | --- | --- | --- | --- | --- |
| `feat/typeahead-hud` | 2026-07-10 | 2026-07-10 (PR#12 `3ff13c1`) | 2026-07-10 | 13+docs | 타입어헤드 완성(전역 KeyDown 합성·HUD·입력 옵션 3종·위치 3×3 피커)·터미널 포커스 강탈 근본 수정·크기 표기·보조 창 아이콘 | [2026-07-10](journal/2026-07-10.md)·[docs/32](32-typeahead-find.md) |
| `feat/panel-terminal-ux` | 2026-07-10 | 2026-07-10 (PR#11 `e81db3b`) | 2026-07-10 | 8+docs | 하단 도크 대원칙(싱글=활성 패널)·분할 위치 복원·터미널에서 열기·터미널(선택 스크롤·우클릭 복사·줄바꿈 옵션)·다크 메뉴+테마 서브메뉴(기본 System) | [2026-07-10](journal/2026-07-10.md) |
| `feat/toolbar-groups` | 2026-07-10 | 2026-07-10 (PR#10 `b230212`) | 2026-07-10 | 11+docs | 도구모음 그룹화(파일 표시 토글)·순서 설정·플랫 메뉴·터미널 위치 이동·Copy as name·메뉴 글꼴 슬롯 | [2026-07-10](journal/2026-07-10.md)·[docs/44 §5](44-toolbar-and-launcher.md) |
| `feat/font-settings` | 2026-07-10 | 2026-07-10 (PR#9 `42168ec`) | 2026-07-10 | 9+docs | 글꼴 5종 슬롯·밀도(행 1px·탭 폰트연동)·설정 창 VS Code식(검색+트리)·i18n 전면(~140키)·QA 5라운드 | [2026-07-10](journal/2026-07-10.md)·[docs/40 §8](40-preferences-system.md) |
| `feat/toolbar-launcher` | 2026-07-09 | 2026-07-09 (PR#8 `9882560`) | 2026-07-09 | 1+docs | 도구 모음(내장 3종) + 퀵 런처(외부 시드 VS Code·exe 아이콘) 슬라이스1 | [2026-07-09](journal/2026-07-09.md)·[docs/44](44-toolbar-and-launcher.md) |
| `fix/copy-as-path-order` | 2026-07-08 | 2026-07-08 (PR#7 `ac84a55`) | 2026-07-08 | 1 | 컨텍스트 대상 경로를 화면 표시 순서로(삽입 순서→가시 인덱스) | [2026-07-08](journal/2026-07-08.md) |
| `fix/copy-as-path-label` | 2026-07-08 | 2026-07-08 (PR#6 `1e79002`) | 2026-07-08 | 1 | 경로 복사 라벨을 셸 원 명칭 "Copy as path"로 | [2026-07-08](journal/2026-07-08.md) |
| `feat/copy-as-path` | 2026-07-08 | 2026-07-08 (PR#5 `1bbce3f`) | 2026-07-08 | 1+docs | 경로 복사 커스텀 항목 — 셸 동사 제자리 대체(교차폴더 전체·Alt POSIX) | [2026-07-08](journal/2026-07-08.md)·[docs/38 §7-5](38-adr-0005-shell-context-menu.md) |
| `feat/i18n-lang-files` | 2026-07-08 | 2026-07-08 (PR#4 `558117a`) | 2026-07-08 | 1+docs | i18n 외부 `.lang` 파일화 — 설치+사용자 폴더·포맷 심(JSON↔properties)·기준 en | [2026-07-08](journal/2026-07-08.md)·[docs/42](42-i18n-language-files.md) |
| `feat/preferences` | 2026-07-08 | 2026-07-08 (`d99c550`) | 2026-07-08 | 3+docs | 설정 인프라(PREF-1 settings.json) + i18n(Localizer·ko/en) + 레이아웃 토글 영속 | [2026-07-08](journal/2026-07-08.md)·[docs/40](40-preferences-system.md) |
| `feat/context-menu-custom` | 2026-07-08 | 2026-07-08 | 2026-07-08 | 3+docs | 커스텀 메뉴 레지스트리(사용자화 설계) + Checksum 서브메뉴(6종) | [2026-07-08](journal/2026-07-08.md)·[docs/38 §7](38-adr-0005-shell-context-menu.md) |
| `feat/theme-system` | 2026-07-08 | 2026-07-08 | 2026-07-08 | 3+docs | 테마 S1(틴트 제거·토큰·라이트/다크) + 상태바 정리 + 가로 스크롤 | [2026-07-08](journal/2026-07-08.md)·[docs/39](39-theme-system.md) |
| `feat/bottom-panel-info` | 2026-07-05 | 2026-07-06 (`3dd423a`) | 2026-07-06 | 9 | 하단 콘텐츠 — 정보/미리보기(플러그인 SDK)·터미널(BP-2/BP-T) | [2026-07-05](journal/2026-07-05.md) |
| `feat/bottom-panel` | 2026-07-05 | 2026-07-05 (`01c85a7`) | 2026-07-05 | 4 | 하단 패널 컨테이너 프레임워크(BP-1) | [worklog](journal/archive/bottom-panel-worklog.md) |
| `refactor/003-audit` | 2026-07-03 | 2026-07-05 (`bd45f86`) | 2026-07-05 | 77 | 3차 감사 — 파일 조작·전송 엔진·정렬·타입어헤드 | [worklog](journal/archive/refactor-003-worklog.md) |
| `refactor/002-audit` | 2026-07-02 | 2026-07-03 (`1d9d312`) | 2026-07-05 | — | 2차 감사 — 트랙 A 성능 + B 구조 | [worklog](journal/archive/refactor-002-worklog.md) |
| `refactor/001-audit` | 2026-07-02 | 2026-07-02 (`b38e6b3`, 태그 `0.1.0`) | 2026-07-05 | — | 1차 감사 + **C1 코어 트리/선택 이관** | [worklog](journal/archive/refactor-001-worklog.md) |

> 참고: 스트레이 로컬 브랜치 `a`(= 002 병합 커밋 `1d9d312`를 가리키던 실수 브랜치, 고유 커밋 0)도 2026-07-05 정리 삭제.

---

## feat/typeahead-hud

- **생성**: 2026-07-10 (분기: main `e81db3b`). **기능 13커밋 + docs**. 병합(PR#12 `3ff13c1`)·삭제: 2026-07-10.
- **작업**: [journal/2026-07-10.md](journal/2026-07-10.md) 상세 · 설계 [docs/32](32-typeahead-find.md)(상태 갱신 완료).
  - **타입어헤드 완성**: 검색어 HUD(`EphemeralOverlay` 신규, `a7c2002`) · 무동작 원인 3단 진단 — ① 패널 클릭 시 그리드 포커스 회수(`5bbd47e`) ② **터미널 포커스 강탈 근본 수정**(`1e2f96e`: 선택 변경→도크 재렌더→`Start`가 매번 포커스 훔침 → 재렌더 경로 `Start(focus:false)`) ③ **CharacterReceived는 포커스 요소 전용(비버블)로 확정 → 전역 KeyDown 합성**(`85f175a`: `TypeAheadKeyToChar` 미국식 배열).
  - **입력 범위 확장**(`0d61a6a`): 파일명 허용 문자 전체 + Space는 접두사 진행 중일 때만.
  - **설정**(`c56dd7e`·`f0a5c0b`·`4dd6a5e`·`f36cf5a`): HUD 위치 9방(3×3 매트릭스 피커 — Border 타일 32px·hover 50%/선택 100%/미선택 회색, Button PointerOver 템플릿 간섭 회피) + 입력 옵션 3종(특수문자/Space/Backspace) 피커 우측 배치 — 전부 영속.
  - **크기 표기**(`3878a5f`): 컬럼=KB/MB/GB 적응(유효 3자리), 정보 패널=Bytes 단복수.
  - **보조 창 아이콘**(`99415e9`): 설정·전송 진행 창 `AppWindow.SetIcon`.
- QA: 실기 라운드 반영(입력 검증 선행 → 원인 3단 특정, HUD hover 유지, 피커 크기 2회 조정). CI: 매 푸시 job green.

## feat/panel-terminal-ux

- **생성**: 2026-07-10 (분기: main `91da3fd`). **기능 8커밋 + docs**. 병합(PR#11 `e81db3b`)·삭제: 2026-07-10.
  (병합 중 gh 일시 오류로 원격 브랜치가 먼저 삭제됐다가 재푸시→PR 재오픈→병합으로 복구.)
- **작업**: [journal/2026-07-10.md](journal/2026-07-10.md) 상세.
  - **하단 도크 대원칙**(`1b62a85`): 듀얼=좌↔좌·우↔우 / 싱글=활성 패널 탭 기준(정보·미리보기·터미널) — 활성/구성 전환 시 자동 재연결.
  - **터미널에서 열기(내장)**(`1b62a85`): 폴더 1개 선택 컨텍스트 메뉴 → 그 패널 터미널 활성+cd(`OpenInEmbeddedTerminal` 공용).
  - **터미널**(`1b62a85`·`6293af4`·`51005b5`·`6d3beb3`·`ece3501`): 선택 드래그 가장자리 자동 스크롤 · 우클릭=선택 복사 · 긴 출력 줄바꿈 없음+설정 "터미널" 카테고리(줄바꿈↔최대 길이 80~1000, 기본 240·영속) · 가로 스크롤바 입력줄 가림(하단 여백)/선택 버블링 차단.
  - **메뉴 바**(`2644b2a`·`ece3501`): 다크 테마 토큰 4종(VS Code식)+Popup 테마 지정 · **2단 서브메뉴 지원** — 테마 3항목 "테마▸" 그룹. **테마 기본값 Light→System**.
  - **분할 위치 저장·복원**(`ca91d51`·`ff53cdd`): 상단 패널·하단 도크 — 숨김 직전 실측 비율 저장→재표시 복원(기본 50:50).
- QA: 실기 라운드 반영(다크 메뉴·비율 복원 2건·스크롤바 2건·탭 전환 관찰). CI: 매 푸시 전 job green.

## feat/toolbar-groups

- **생성**: 2026-07-10 (분기: main `3ef016c`). **기능 11커밋 + docs**. 병합(PR#10 `b230212`)·삭제: 2026-07-10.
- **작업**: [journal/2026-07-10.md](journal/2026-07-10.md) 상세 · 설계 반영 [docs/44 §5](44-toolbar-and-launcher.md).
  - **도구 모음 그룹화**(`9fad6bd`): 그룹 레지스트리(도구·파일 표시)+세로 구분선. 파일 표시 토글 2종(숨김=RedEye/Hide 교체·도트파일=⋯) — `SetShow*` 단일 경로로 메뉴/설정/배경 메뉴 동기. `ToolbarOptions` 영속.
  - **순서 설정**(`5d50169`): 설정 "도구 모음" — 그룹/항목 ▲▼(즉시 반영·나열 id 우선 규칙).
  - **플랫 메뉴**(`52f2e63`): `NexaMenuPresenterStyle`(사각) — 빈영역(섹션+표시 토글 ✓+단축키 컬럼)·탭(3섹션+Ctrl+W).
  - **메뉴 바 정렬**(`530be01`): 체크 칸 18px 전 항목 상시 예약 — 텍스트 시작 위치 통일.
  - **터미널 위치 이동**(`7ab39ae`·`3b4224f`·`2d1b12a`·`39ad313`): 도구 모음 밀착 정사각 + 하단 도크(터미널 토글과 한 몸·터미널 활성 시 초록) — 활성 탭 폴더 cd(cmd `/d`·pending 전송).
  - **복사 확장**(`7ab39ae`·`12f2207`·`5f26711`): Copy as name(파일·탭 공용, ko 이름 복사)·path 위 동반 삽입(`VerbReplacement.Before`)·라벨 i18n.
  - **컨텍스트 메뉴 글꼴**(`4b5d482`): MenuFamily/Size 슬롯 — 플랫 메뉴 적용(셸 HMENU는 OS 글꼴).
  - **버그**(`c304097`): 탭 전환 시 도크 현재 폴더 미갱신(터미널 cd가 이전 폴더로) — `SwitchToTab`에서 `RefreshBottomDocks`.
- QA: 실기 라운드 반영(메뉴 정렬·버튼 위치 정정·한 몸 스타일·탭 전환 버그 사용자 관찰 특정). CI: 매 푸시 전 job green.

## feat/font-settings

- **생성**: 2026-07-10 (분기: main `ca09ff0`). **기능 9커밋 + docs 5커밋**. 병합(PR#9 `42168ec`)·삭제: 2026-07-10.
- **작업**: [journal/2026-07-10.md](journal/2026-07-10.md) 상세 · 설계 반영 [docs/40 §8](40-preferences-system.md).
  - **글꼴 5종 슬롯**(`432cf44`→`6f6ba53`): 기본(메뉴·탭·경로·하단)/콘솔(쉼표 폴백·셀 폭 실측)/상태바/파일 목록/헤더 꾸미기 + 폴더 굵게. `FontOptions`↔`FontSettings` 영속·`ApplyFonts` 라이브. App.xaml 암시적 TextBlock 스타일 제거(Style이 상속을 이겨 설정 폰트 무시 → 근본 해소). 경로 슬롯은 도입 후 기본 글꼴로 통합·제거(사용자 결정). 탭 높이=기본 글꼴 크기 연동.
  - **밀도**(`ae7ad0b`·`96c5cfe`): 행 상하 1px·행간 0 + 행 -1px 겹침(선택 박스 4면 유지·경계 1px)·탭 20px·stride 동기.
  - **설정 창 VS Code식**(`e078774`·`2db69f3`): 좌측 검색+카테고리 트리+설정 레지스트리(그룹/항목 이중 필터), 영속 설정 전부 UI 수록(+`Sort.FoldersFirst` 신규·dwell 라이브 재반영). 글꼴 입력=편집 콤보(목록+직접 입력, 엔터/포커스 이탈 검증 경고·Loaded 전 Text 무시 버그 수정).
  - **i18n 전면**(`3819762`): 키 ~140종 3벌(ko/en/.json) — 상태바·컨텍스트/탭 메뉴·다이얼로그·전송/덮어쓰기 창·드래그 캡션·정보창·ViewModels/휴지통 예외까지. 잔여=개발자 진단뿐.
  - **경로 바**(`c6e1f67`·`c6e7b4d`): 편집 모드 높이(브레드크럼 실측 고정)·글꼴(컨트롤 폰트 복사) 일치.
- QA: 실기 5라운드(검색 UX·콤보 초깃값·테두리 2회·경로바 2건·기본 글꼴 통합) 반영. CI: 매 푸시 전 job green(WinUI 앱 빌드 포함)·xUnit 81.

## feat/copy-as-path

- **생성**: 2026-07-08 (분기: main `dbdfc64`). **1커밋 + docs**. 병합(PR#5 `1bbce3f`)·삭제: 2026-07-08.
- **작업**: **경로 복사(Copy as path)** 커스텀 항목 — 교차폴더 다중선택에서 셸 "Copy as path"가 단일 폴더만 복사하던 문제([docs/38 §7-5](38-adr-0005-shell-context-menu.md)).
  - `ShellContextMenu.VerbReplacement` — HMENU의 canonical verb(`copyaspath`) 위치를 `FindMenuPosByVerb`로 찾아 `DeleteMenu`+`InsertMenuW`(MF_BYPOSITION)로 **제자리 대체**.
  - `CmItemDef.ReplaceVerb` — 해당 항목은 셸 섹션 대신 대체 목록으로 분리(못 찾으면 하단 폴백).
  - `CopyPathsAsText(Targets, alt)` — 축소 이전 전체 선택을 클립보드로. 기본=따옴표+역슬래시, **Alt=POSIX(`/`)**(여는 시점 `CmCtx.Alt`).
- 동반 문서(main 직접): [docs/43](43-external-files-and-config.md) 외부 파일·환경값 레퍼런스.
- CI: 전 job green(WinUI 앱 빌드 포함).

## feat/i18n-lang-files

- **생성**: 2026-07-08 (분기: main `af7a194`). **1커밋 + docs**. 병합(PR#4 `558117a`)·삭제: 2026-07-08.
- **작업**: i18n 문자열을 **외부 `.lang` 파일**로 분리([docs/42](42-i18n-language-files.md)) — 재빌드 없이 언어 추가·수정.
  - **포맷 심**(`ILangFormat`): JSON 활성 + properties 구현, `LangFormats.Active` 한 줄 전환(UDF 엔진 스왑 패턴). 대등성 테스트로 무손실 보증.
  - **2계층 폴더**: 설치 `<exe>/lang/` + 사용자 `%APPDATA%/NexaDir/lang/`(오버라이드 우선), 임베디드 en 안전망.
  - 순수 계층(`LangFile`/포맷 → ViewModels **xUnit +7, 총 81**), 앱 계층(`LangCatalog`·`Loc.Init`·설정 언어 페이지 동적화).
- **결정**: 포맷=JSON(전환 심)·사용자 오버라이드 폴더 도입·기준 언어=영어.
- CI: 전 job green(WinUI 앱 빌드 포함).

## feat/preferences

- **생성**: 2026-07-08 (분기: main `1b89b43` 설계 커밋 이후). **3커밋 + 저널**. 병합(`d99c550`, --no-ff)·삭제: 2026-07-08.
- **작업**: **설정(Preferences) 시스템**([docs/40](40-preferences-system.md)) —
  - **PREF-1**(`3ad019c`): `SettingsStore`(settings.json 로밍·`SessionStore` 규율 재사용) + `Ctrl+,` 설정 창(모양/레이아웃/메뉴/언어) — 재시작 소실되던 옵션 4벌(테마·표시·메뉴·탭) 영속.
  - **i18n**(`91fa79b`, PREF-8/D-2): 순수 `Nexa.ViewModels.Localizer`(폴백 체인, **xUnit 7·총 74 green**) + JSON 문자열 테이블(ko/en 임베디드) + `LocExtension` 마크업 확장 + 메뉴 바 전면 이관 + 언어 페이지(재시작 전환).
  - **레이아웃 영속**(`7b861b1`, PREF-3 부분): 퀵 런처·우 패널 토글 → `SessionState.Layout`(머신 로컬).
- QA: 설정 영속·**영어 메뉴 전환 스크린샷 확인**.

## feat/context-menu-custom

- **생성**: 2026-07-08 (분기: main `3fe180f`). **3커밋 + docs**. 병합·삭제: 2026-07-08.
- **작업**: 컨텍스트 메뉴 **커스텀 항목 사용자화 설계 변경**([docs/38 §7](38-adr-0005-shell-context-menu.md)) — 선언적 레지스트리(`CmItemDef`, Children 서브메뉴)+설정 스키마(`MenuOptions`: 표시/순서/섹션 위치, 설정 UI 후속) · **Checksum ▶ MD5/SHA-1/256/384/512/CRC32**(파일별 줄·폴더 제외·복사 대화상자) · 다중 파일 1줄 표시 버그 수정 · 인코딩(Base64)은 구현 후 사용자 결정으로 제거.
- QA: 3파일 3줄·서브메뉴·기존 항목 회귀 없음 확인.

## feat/theme-system

- **생성**: 2026-07-08 (분기: main `379a8aa`). **3커밋 + docs**. 병합·삭제: 2026-07-08.
- **작업**: **테마 시스템 S1**([docs/39](39-theme-system.md)) — 영역 구분 틴트 전면 제거 → 시맨틱 토큰 10종(App.xaml ThemeDictionaries)·라이트 팔레트 정비·구성(O) 메뉴 라이트/다크/시스템 전환(기본 Light)·후속 세부 설정 UI(테마팩·폰트·밀도) 설계.
- **동반**: 패널 보기 토글 → 표시(S) 메뉴 이관(상태바=상태 전용, `c5df6b9`) · 파일 목록 **가로 스크롤**(넘칠 때만·헤더 양방향 동기, `a099352`).
- 사용자 QA: 라이트 배치 확인("전반적으로 깔끔").

## feat/bottom-panel-info

- **생성**: 2026-07-05 (분기: main `01c85a7` 이후). **9 커밋**.
- **작업**: 하단 패널 **콘텐츠 실체화**.
  - **BP-2 정보/미리보기**: 정보 뷰(선택 항목 속성) · **미리보기 시스템**(표준 `IPreviewProvider`+`PreviewRegistry`, 텍스트/이미지) ·
    **퍼미시브 MIT 플러그인 SDK `Nexa.Plugins`**(DR-6)+샘플 `Nexa.Plugins.Samples`+**개발 매뉴얼**([36](36-plugin-development.md)) ·
    크기 상호연동(`PreviewRequest`) · **로딩 부하 방지 wrapper**(디바운스/취소/중복스킵) · 텍스트 1줄 버그·가로세로 스크롤 수정.
  - **BP-T 터미널**: **ConPTY 세션** + **VT 에뮬레이터**(`VtScreen`: 색·화면 버퍼·커서·SGR·스크롤백) · **lazy 로딩** · **exit 시 재시작** ·
    작업경로=활성 탭 폴더(홈 폴백) · **키보드 캡처**(전역 단축키 개입 차단). 알려진 이슈 [BUG-007/008](BUGS.md)(캐럿·색, BP-T3).
- **병합**: 2026-07-06 (`3dd423a`) → main. 코어·앱 빌드 green.
- **삭제**: 2026-07-06 (로컬 + 원격 `origin/feat/bottom-panel-info`).
- **상세**: [journal/2026-07-05.md](journal/2026-07-05.md) · 설계 [35](35-preview-system.md)·[36](36-plugin-development.md)·[37](37-terminal.md).

## feat/bottom-panel

- **생성**: 2026-07-05 17:47 (첫 커밋 `133dc0d`). 분기: `bd45f86`(003 병합 직후).
- **작업**: **하단 패널 컨테이너 프레임워크(BP-1)** — placeholder → 실제 콘텐츠 호스트. 콘텐츠 종류 선택(정보/미리보기/
  Hex/터미널)·스왑(정보=현재 폴더 실제, 나머지 준비 중) · **Ctrl+\` 표시/숨김 토글** · 하단 패널 상태(표시/높이/좌우
  분리/콘텐츠 종류) **session.json 저장·복원**. 미리보기/Hex/터미널(ConPTY)은 후속(BP-2/BP-T).
- **커밋 이력** (4):
  - `133dc0d` docs(bottom-panel): 하단 패널 구현 브랜치 작업 로그 — 분해·BP-1 계획
  - `8710afa` feat(app): 하단 도킹 콘텐츠 호스트 BottomDockView (BP-1a)
  - `306f476` feat(app): 하단 패널 Ctrl+\` 토글 + 상태 세션 저장/복원 (BP-1b/1c)
  - `704d125` docs(bottom-panel): BP-1 프레임워크(호스트·Ctrl+\` 토글·세션 저장) 진행 기록
- **병합**: 2026-07-05 18:02 (`01c85a7`) → main. 코어·앱 빌드 green.
- **삭제**: 2026-07-05 (로컬 + 원격 `origin/feat/bottom-panel`).
- **상세**: [journal/archive/bottom-panel-worklog.md](journal/archive/bottom-panel-worklog.md).

## refactor/003-audit

- **생성**: 2026-07-03 20:07 (첫 커밋 `7997efc` — 3차 검증 4축 감사). 분기: `1d9d312`(002 병합 직후). **77 커밋**.
- **작업**: 3차 감사 라운드 — **파일 조작 계층**(인라인 이름변경 B-6 · 컨텍스트 메뉴 · 복사/이동/삭제 · 단축키 ·
  DnD 폴더 이동/좌우/탭·폴더 hover/ESC/디스크별 · 휴지통 삭제) · **DnD 탐색기 파리티**(라이브 캡션·자기폴더 규칙) ·
  **헤더 정렬 COL-2/3**(3상태·다중열·좌우 독립·ABI v6) · **확장자/날짜·시간 컬럼** · **타입어헤드**(코어 find_prefix·버퍼·배선) ·
  **watcher 1차** · **탭 세션 저장/복원**(session.json, 요청/수행 분리·Tick 코얼레싱) · **경로 바 환경변수 해석** ·
  **전송 단일 엔진 `TransferPathsInto`**(덮어쓰기 확인·바이트 진행률·진행 창·취소) · **OS 클립보드 붙여넣기** ·
  **새로 만들기**(폴더/파일/바로가기) · **새 PC 식별 문서**.
- **병합**: 2026-07-05 17:31 (`bd45f86`) → main. 로컬 빌드·테스트 green(코어 · ViewModels xUnit 57 · 앱 0/0).
- **삭제**: 2026-07-05 (로컬 + 원격 `origin/refactor/003-audit`).
- **상세 커밋 이력**: [journal/archive/refactor-003-worklog.md](journal/archive/refactor-003-worklog.md).

## refactor/002-audit

- **생성**: 2026-07-02 23:32 (첫 커밋 `5d676ae` — 진행 로그 스캐폴드). 분기: `b38e6b3`(001 병합 직후).
- **작업**: 2차 감사(전체 개발범위 5축) → **트랙 A 성능**(A-3 경로→NodeId ABI v5 · A-1 백그라운드 열거 · A-2 범위 diff 통지 ·
  A-4 아이콘 LRU 캐시+로딩 큐) + **트랙 B 구조**(PanelView 그룹 객체 · `Nexa.ViewModels`(net8.0) 추출 + C# xUnit 도입) + 문서·QA. BUG-001 해결.
- **병합**: 2026-07-03 19:51 (`1d9d312`) → main.
- **삭제**: 2026-07-05 (원격 `origin/refactor/002-audit`).
- **상세 커밋 이력**: [journal/archive/refactor-002-worklog.md](journal/archive/refactor-002-worklog.md).

## refactor/001-audit

- **생성**: 2026-07-02 13:19 (첫 커밋 `41bb9e2` — 통합 감사 진단). 분기: main(스캐폴딩 이후).
- **작업**: 1차 통합 감사(소스·문서·진행 정합) → **C1 코어 트리/선택을 Rust 코어(`nexa-tree`)로 이관** — 가시행 평면
  스트림 + OrderedSet 선택, C ABI `nexa_tree_*`, 호스트 ABI 안전 계층, 앱 가상화 재배선(펼침 유지·스크롤 복원),
  10만 노드 코어 벤치, 탭별 트리 핸들 캐시, id→가시 인덱스 조회, 죽은 코드 정리.
- **병합**: 2026-07-02 23:20 (`b38e6b3`) → main. 병합 전 베이스라인에 태그 `0.1.0`.
- **삭제**: 2026-07-05 (원격 `origin/refactor/001-audit`).
- **상세 커밋 이력**: [journal/archive/refactor-001-worklog.md](journal/archive/refactor-001-worklog.md).
