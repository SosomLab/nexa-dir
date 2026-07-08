# Nexa Dir 언어 파일(`lang/*.lang`)

이 폴더의 `*.lang` 파일이 앱 UI 문자열(i18n)입니다. **재빌드 없이** 추가·수정할 수 있습니다. 설계 → [docs/42](../../../docs/42-i18n-language-files.md).

## 위치(로드 우선순위)

1. **사용자**: `%APPDATA%\NexaDir\lang\*.lang` — 추가·오버라이드(업데이트가 안 지움, **우선**)
2. **설치**: `<앱 폴더>\lang\*.lang` — 배포 기본
3. **임베디드 en** — 최후 안전망(위가 모두 없을 때만)

같은 코드가 사용자·설치 양쪽에 있으면 **키 단위로 사용자 값이 우선**(일부만 덮어쓰기 가능).

## 형식(현재: JSON)

평탄 JSON. `@` 접두 키 = 메타데이터, 나머지 = 문자열. UTF-8(BOM 무관).

```json
{
  "@code": "xx",           // 필수: 언어 코드(ko, en, ja …)
  "@name": "자기표기",      // 필수: 설정 목록에 노출
  "@name.en": "English name",
  "@app": "0.2.0",         // 대상 앱 버전(구버전 경고용)
  "@fallback": "en",
  "menu.file": "…"
}
```

> 포맷은 `Nexa.ViewModels.I18n.LangFormats.Active` 한 줄로 properties(`키 = 값`, 주석 `#`)로 전환 가능(확장자 `.lang` 유지). 누락 키는 `@fallback`(기본 en) → 키 문자열로 표시됩니다.

## 새 언어 추가

1. `en.lang`를 복사해 `xx.lang`로 저장.
2. `@code`/`@name` 수정 + 값 번역.
3. 앱 재시작 → 설정 › 언어에 자동 노출.
