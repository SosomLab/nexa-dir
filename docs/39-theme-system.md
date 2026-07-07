# 39 · 테마 시스템 — 라이트/다크 모드 + 시맨틱 색 토큰 (설계 + S1 구현)

> 상태: **S1 구현**(2026-07-08, `feat/theme-system`) — 토큰화 + 라이트 팔레트 + 모드 선택 메뉴.
> 관련: [20 UI 레이아웃](20-ui-layout.md) · [34 설정/세션](34-settings-and-session-persistence.md) · DR-2(프로툴 고밀도·다크 기본).

## 1. 배경 (문제)

초기 레이아웃 단계에서 **영역 구분을 위해 서로 다른 틴트 배경**(도구모음=파랑, 런처=초록, 좌 패널=파랑, 우 패널=빨강,
하단 도킹=청록/자홍, 상태바=회색)을 임시로 깔았다. 기능이 채워진 지금은 이 틴트가 제품 인상을 해치고,
색이 코드 곳곳에 하드코딩되어 테마 전환이 불가능하다.

## 2. 목표

1. **틴트 제거** — 영역 구분은 색이 아니라 **경계선 + 중립 배경의 명도 차**로.
2. **라이트/다크 모드 선택**(구성 메뉴 → 후속 설정 UI) — 우선 **라이트 팔레트 정비**.
3. 모든 chrome 색을 **시맨틱 토큰**으로 승격 — 후속 "테마팩"(색·폰트·크기 오버라이드)의 기반.

## 3. 아키텍처

- **WinUI `ThemeDictionaries`** (App.xaml): `Light`/`Dark` 키 아래 **시맨틱 브러시 토큰** 정의.
  컴포넌트는 `{ThemeResource Nexa*Brush}`만 참조 — 모드 전환 시 자동 재해석.
- **모드 적용**: `Window.Content.RequestedTheme` = `AppSettings.Theme.Mode`(System/Light/Dark).
  시스템 = `ElementTheme.Default`(OS 설정 추종).
- **설정**: `ThemeOptions { Mode }` (인메모리 → settings.json 영속은 docs/34 설정 시스템에 합류).
  현 브랜치 기본 **Light**(라이트 팔레트 검증용). DR-2 "다크 기본"은 다크 팔레트 정비 후 기본값 재결정.
- **예외(테마 비대상)**: 임베디드 터미널(다크 고정 — 터미널 정체성) · 드래그 캡션/글리프(OS 렌더) ·
  행 선택/호버 브러시(알파 기반 — 양 모드 공용, 후속 토큰화 후보).

## 4. 시맨틱 토큰 (S1)

| 토큰 | 용도 | Light | Dark |
|---|---|---|---|
| `NexaWindowBackgroundBrush` | 창 루트 | `#F6F7F9` | `#14161A` |
| `NexaChromeBackgroundBrush` | 메뉴·도구모음·런처 | `#EEF1F5` | `#1E2228` |
| `NexaPanelBackgroundBrush` | 파일 패널(좌/우 동일) | `#FFFFFF` | `#191C21` |
| `NexaTabBarBackgroundBrush` | 탭 바(좌/우 동일) | `#E6EAF0` | `#232830` |
| `NexaHeaderBackgroundBrush` | 컬럼 헤더 | `#E9ECF1` | `#262B33` |
| `NexaFieldBackgroundBrush` | 경로바 등 입력 필드 컨테이너 | `#FFFFFF` | `#262B33` |
| `NexaBottomDockBackgroundBrush` | 하단 도킹(좌/우 동일) | `#F1F3F6` | `#1B1F25` |
| `NexaStatusBarBackgroundBrush` | 상태바 | `#EEF1F5` | `#1E2228` |
| `NexaBorderBrush` | 영역 경계선·스플리터 | `#D5DAE1` | `#363C46` |
| `NexaAccentBrush` | 강조(활성 탭 줄·삽입 표시 등) | `#3D8BFF` | `#3D8BFF` |

컴포넌트 매핑: 기존 하드코딩 → 토큰 치환(MainWindow.xaml 전 영역). 탭 활성/비활성 배경(PanelTab)은
중립 알파(`활성=accent 33%`, `비활성=회색 8%`)로 코드 유지 — 양 모드 공용(후속 토큰화).

## 5. 후속 — 테마 세부 설정 UI (설계 스케치)

> 목표: 테마별로 **색상·폰트·크기(밀도)** 를 세부 설정. 설정 창(도구 docs/34)의 "모양(Appearance)" 페이지.

1. **모드**: 시스템/라이트/다크 라디오(= 현 메뉴 토글의 UI 이관).
2. **테마팩**: 내장 프리셋(Light/Dark/HighContrast/…) + 사용자 팩. 스키마:
   `ThemePack { Name, BaseMode, Dictionary<string tokenKey, string colorHex> Overrides }`.
   적용 = 해당 모드 `ThemeDictionaries`에 오버라이드 딕셔너리 **런타임 병합**(리소스 교체 후 RequestedTheme 재설정으로 재해석 유도).
3. **폰트**: 기본 UI 폰트(현 Segoe UI, `ContentControlThemeFontFamily`/암시적 TextBlock 스타일 교체) ·
   모노(터미널/미리보기 — 후속 Nerd Font와 연계, BUG-008).
4. **크기(밀도)**: 행 높이·기본 폰트 크기 스케일(프로툴 고밀도 기본, Comfort 옵션) — 컬럼 시스템(docs/23)과 연계.
5. 저장: `settings.json`(docs/34) `Theme` 섹션. 마이그레이션: 토큰 키는 안정 계약(rename 시 마이그레이션 표).

## 6. S1 구현 내역

- App.xaml `ThemeDictionaries`(§4 토큰) · Settings `ThemeOptions.Mode`(기본 Light) ·
  구성(O) 메뉴 "테마 ▶ 시스템/라이트/다크"(라디오식 체크) · `ApplyTheme()`(RequestedTheme).
- MainWindow.xaml 전 틴트 제거 → 토큰 치환(+영역 경계선), 그리드 `HeaderBackground`를 토큰으로 바인딩,
  `NexaFileGrid` 헤더 구분선 중립화(`#33FFFFFF`→`#33808080`), PanelTab 탭 배경 중립 알파화.
