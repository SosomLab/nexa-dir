# 42 · i18n 외부 언어 파일(`.lang`) 설계 — 설치 폴더 기반 다국어 관리

> 목표: 지금 **DLL에 임베디드**(재빌드 필요)로 박혀 있는 i18n 문자열 테이블을,
> **설치 폴더의 언어별 `.lang` 파일**로 분리해 **재빌드 없이 언어 추가·수정·번역가 참여**가 가능하게 한다.
> 관련: PREF-8(언어)·D-2([TODO](TODO.md)) · 현행 인프라 [40 §언어](40-preferences-system.md) · [Loc.cs](../app/Nexa.App/Loc.cs)·[Localizer.cs](../app/Nexa.ViewModels/Localizer.cs).
>
> **구현 상태(2026-07-08 `feat/i18n-lang-files`)**: ✅ 구현 완료. **결정 반영 — 포맷=JSON(현 활성), `ILangFormat` 심으로 properties 전환은 [`LangFormats.Active`](../app/Nexa.ViewModels/I18n/LangFormats.cs) 한 줄 교체 · 사용자 오버라이드 폴더 도입 · 기준(base) 언어=영어.** 파일 확장자 `.lang`는 **포맷 무관**(내용만 해당 포맷).

---

## 1. 현재(임베디드) → 한계

| 구분 | 현행 | 문제 |
| --- | --- | --- |
| 문자열 출처 | `Nexa.App.dll` 임베디드 리소스 `Strings/{code}.json` | 언어 추가·오타 수정도 **재빌드** 필요 |
| 지원 언어 목록 | `Loc.Supported = {"ko","en"}` **하드코딩** | 새 언어 = 코드 수정 |
| 번역가/사용자 | 참여 경로 없음 | 커뮤니티/현지화 불가 |
| 설정값(어떤 언어) | `settings.json` `General.Culture` (유지) | — (변경 없음) |

> **불변**: 마크업 확장(`{loc:Loc}`)은 **XAML 파싱 시점 정적 조회**라 언어 전환은 **재시작 반영**. 이 설계는 문자열의 *출처*만 바꾸며 이 제약은 그대로다(코드 `Loc.T`는 즉시).

## 2. 목표 구조 — 설치 폴더 + 사용자 폴더 2계층

```
<설치 폴더>/                     (예: C:\Program Files\Nexa Dir\  또는 포터블 앱 폴더)
  Nexa.App.exe
  lang/                          ← ★ 기본 제공 언어 파일(배포 산출물)
    ko.lang
    en.lang
    ja.lang        (추가 예시)

%APPDATA%\NexaDir\lang/          ← ★ 사용자 추가/오버라이드(선택) — 업데이트가 덮지 않음
    fr.lang        (사용자가 직접 넣은 새 언어)
    ko.lang        (기본 ko의 일부 키만 덮어쓰기 가능)
```

- **설치 폴더 `lang/`**: 배포에 포함되는 공식 언어. 앱 업데이트 시 갱신됨.
- **사용자 `%APPDATA%\NexaDir\lang/`**: 사용자/번역가가 추가·수정. **업데이트에 안 지워짐**. 같은 코드가 양쪽에 있으면 **사용자 파일이 키 단위로 우선**(부분 오버라이드).
- **임베디드 `en`(안전망 유지)**: `en.json`(또는 `en.lang`)만은 **DLL에도 임베드 유지** → `lang/` 폴더가 통째로 없거나 깨져도 UI가 빈 키로 붕괴하지 않게 하는 **최후 폴백**.

### 로드 우선순위(키 단위 병합)

```
사용자 lang/{code}.lang  ▶  설치 lang/{code}.lang  ▶  (code==선택언어일 때)
     └─ 선택 언어 미스 시 ─▶ 사용자/설치 lang/en.lang  ▶  임베디드 en(안전망)  ▶  키 문자열 그대로
```

`Localizer`의 기존 폴백 체인(현재 → fallback → 키)을 그대로 쓰되, **"현재 테이블"과 "fallback 테이블" 자체를 파일 병합 결과로 구성**한다.

## 3. `.lang` 파일 형식 — 번역가 친화 properties 스타일

JSON 대신 **주석·헤더가 가능한 라인 기반 형식**을 채택(번역 중 콤마·따옴표 이스케이프 실수 방지, diff 친화, 문맥 주석 가능).

```ini
# Nexa Dir 언어 파일 — 주석은 '#' 로 시작
@code    = ko            # 필수: BCP-47 소문자 코드(ko, en, ja, pt-br …)
@name    = 한국어         # 필수: 자기 언어 표기(설정 화면 목록에 그대로 노출)
@name.en = Korean        # 권장: 영어 표기(중립 정렬/식별용)
@author  = SosomLab
@app     = 0.2.0         # 대상 앱 버전(구버전 번역 경고용)
@fallback= en            # 이 언어에서 누락 시 참조할 코드(기본 en)

# ── 메뉴 바 ──
menu.file            = 파일(F)
menu.file.newTab     = 새 탭
menu.file.exit       = 종료

# 개행은 \n, 역슬래시는 \\ 로 이스케이프
preferences.restartHint = 언어 변경은\n재시작 후 완전히 반영됩니다.
```

**파싱 규칙**
- `#` 로 시작하는 줄 = 주석, 빈 줄 무시.
- `@key = value` = 헤더 메타데이터. `key = value` = 문자열 항목.
- 첫 `=` 기준 분리, 키/값 **trim**. 값 내 이스케이프: `\n`(개행), `\t`(탭), `\\`(역슬래시). 그 외는 리터럴.
- **인코딩 UTF-8(no BOM) 강제** — CJK 필수. (BOM 있으면 첫 키 오염되므로 로더가 BOM 스킵.)
- 중복 키 = 마지막 값 승리(관용).

> **대안(선택지)**: 형식을 **기존 JSON 유지(`.lang`=JSON)** 로 가면 파서 불필요(현 `LoadTable` 재사용). 대신 주석·헤더 메타·번역가 편의는 포기. → 권장은 properties 스타일(파서 ~40줄), 단 결정은 착수 시 확정.

## 4. 구현 변경점

### 4-1. `LangCatalog`(신규, 앱 계층) — 언어 파일 탐색·파싱
- `IEnumerable<LangInfo> Discover()` — 설치 `lang/` + 사용자 `lang/` 스캔 → `{Code, Name, NameEn, Author, App, Path[]}` 목록(중복 코드는 소스 표시). **설정 화면 언어 목록의 원천**(하드코딩 `Supported` 제거).
- `IReadOnlyDictionary<string,string> Load(code)` — 해당 코드의 (설치 → 사용자) 파일을 **키 단위 병합**해 반환. 파일 없으면 null.
- `.lang` 파서(정적) — §3 규칙. 예외는 격리(깨진 줄 스킵·경고 로그).

### 4-2. `Loc.Init` 재작성 — 출처를 파일로
```
Init(cultureSetting):
    code     = Resolve(cultureSetting, LangCatalog.Discover())   # 목록도 동적
    table    = LangCatalog.Load(code)    ?? EmbeddedTable(code)   # 파일 우선, 없으면 임베디드
    fallback = code=="en" ? null
             : (LangCatalog.Load("en")   ?? EmbeddedTable("en"))
    Localizer.SetCurrent(new Localizer(code, table, fallback))
```
- `Resolve()` 의 지원 판정을 **하드코딩 배열 → 발견된 코드 집합**으로 교체.
- `EmbeddedTable("en")` = 현행 임베디드 로더(안전망 전용으로 축소, `en`만 유지 가능).

### 4-3. 설정 "언어" 페이지 — 동적 목록
- 라디오를 **`LangCatalog.Discover()` 결과로 생성**: "시스템" + 발견된 각 언어(`@name` 표기, 툴팁에 `@name.en`·출처·`@app` 불일치 경고).
- 선택 → `General.Culture = code` 저장(현행과 동일) → 재시작 안내.

### 4-4. 패키징([csproj](../app/Nexa.App/Nexa.App.csproj))
- `ko.lang`·`en.lang` 을 `<Content ... CopyToOutputDirectory="PreserveNewest">` 로 `lang/` 에 배치(빌드 출력·MSIX·포터블 모두 포함).
- `en` **임베디드는 유지**(안전망). 나머지 임베디드는 제거 가능.
- 문서화: `lang/README` — 형식·기여 방법(번역 PR 가이드).

## 5. 엣지 케이스·정책

| 상황 | 처리 |
| --- | --- |
| `lang/` 폴더 통째 없음 | 임베디드 `en` 로 부팅(붕괴 방지) |
| 선택 언어 파일 삭제됨 | `en` 폴백 → 그래도 없으면 임베디드 |
| `.lang` 일부 줄 파손 | 해당 줄만 스킵 + 경고 로그, 나머지 로드 |
| 사용자 파일이 기본 일부만 정의 | 정의된 키만 오버라이드, 나머지는 설치본 |
| `@app` 버전 < 현재 앱 | 로드하되 설정 화면에 "번역 구버전" 배지(신규 키 누락 가능 안내) |
| 잘못된 `@code` | 파일 무시(목록 제외) + 로그 |
| BOM/비UTF-8 | BOM 스킵; 그 외 인코딩은 미보장(문서에 UTF-8 명시) |

## 6. 마이그레이션 단계(무중단)

1. **파서 + `LangCatalog`** 추가(임베디드 경로는 그대로 두고 파일 우선만 얹기) — 회귀 0.
2. 기존 `ko.json`/`en.json` → `ko.lang`/`en.lang` 변환(스크립트) + csproj `Content` 배치.
3. `Loc.Init`·언어 페이지를 카탈로그 기반으로 전환. `Supported` 하드코딩 제거.
4. 임베디드는 `en`만 안전망으로 축소.
5. `lang/README`(형식·기여 가이드) + docs/40 언어 절 갱신 + [docs/18](18-build-and-test.md) 산출물에 `lang/` 추가.

> **테스트**: 파서·병합·폴백은 **순수 로직 → `Nexa.ViewModels`(또는 신규 순수 클래스)로 추출해 xUnit**(맥/Win). 파일 I/O(Discover)만 앱 계층.

## 7. 결정(확정 · 2026-07-08)

- **형식**: **JSON 활성**. `ILangFormat`(`Json`/`Properties`) 심 + `LangFormats.Active` 단일 전환점 → properties로 한 줄 교체. 두 포맷 모두 구현·**대등성 테스트**로 무손실 전환 보장.
- **사용자 오버라이드 폴더**: **도입** — `%APPDATA%/NexaDir/lang/`, 설치본 위로 키 단위 우선.
- **기준 언어**: **영어(en)** — 폴백·임베디드 안전망·미지원 시 기본. ko는 첫 번역으로 동봉.

## 8. 실제 구현 매핑

| 구성 | 파일 | 계층 |
| --- | --- | --- |
| 중립 표현 | [`LangFile`](../app/Nexa.ViewModels/I18n/LangFile.cs)(Meta/Strings + `MergeStrings`) | ViewModels(순수·테스트) |
| 포맷 심 | [`ILangFormat`](../app/Nexa.ViewModels/I18n/ILangFormat.cs)·`JsonLangFormat`·`PropertiesLangFormat`·[`LangFormats`](../app/Nexa.ViewModels/I18n/LangFormats.cs) | ViewModels(순수·테스트) |
| 파일 탐색·병합 | [`LangCatalog`](../app/Nexa.App/LangCatalog.cs)(Discover/Load, 설치+사용자) | 앱(파일 I/O) |
| 부트스트랩 | [`Loc.Init`](../app/Nexa.App/Loc.cs)(파일→임베디드 en 폴백) | 앱 |
| 설정 목록 | [`PreferencesWindow`](../app/Nexa.App/PreferencesWindow.xaml.cs) 언어 페이지(카탈로그 동적) | 앱 |
| 배포 | `lang/en.lang`·`lang/ko.lang`(csproj `Content`) + `en.json` 임베디드 안전망 | — |
| 테스트 | [`LangFormatTests`](../app/Nexa.ViewModels.Tests/LangFormatTests.cs)(파리티·병합·메타·스킵, xUnit 7) | — |

> **번역 추가 방법**: `<설치>/lang/xx.lang`(또는 사용자 `%APPDATA%/NexaDir/lang/xx.lang`)에 `@code=xx`·`@name=…` + 키 복사 → 재시작. 설정 언어 목록에 자동 노출.
