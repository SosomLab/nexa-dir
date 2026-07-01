# 23 · 컬럼 시스템 (파일 보기)

> `NexaFileGrid`/`FileTreeGrid`([21](21-adr-0002-fileview-control.md))의 **컬럼을 데이터로 정의**하는 설계.
> 기본열·사용자 정의(수식)열·플러그인열, 클라우드 상태, 듀얼 패널 동기화를 모두 한 구조로 지원한다.

## 1. 컬럼은 "데이터"
컬럼을 하드코딩하지 않고 `IColumn` 정의 + `ICellValueProvider`(값 공급)로 다룬다 → 런타임 추가/제거/순서/너비/표시/정렬, 직렬화(저장·복원·동기).

```
IColumn
  string  Id / Header
  double  Width · bool Resizable · bool Sortable · bool Visible
  Align   Align · Format Format         // 우측정렬·크기/날짜 포맷 등
  Kind    Kind { BuiltIn, Computed, Plugin }
ICellValueProvider
  CellValue Get(NodeId row, string columnId)   // 표시 텍스트/아이콘/정렬키
ColumnLayout
  IReadOnlyList<IColumn> Columns                // 순서 포함
  Serialize()/Deserialize()                     // 저장·복원·동기 단위
```
- 컨트롤(`NexaFileGrid`)은 **컬럼 의미를 모름** — `IColumn`/`ICellValueProvider`만 안다(도메인 비종속, ADR-0002 §9).

## 2. 컬럼 종류

### 2-1. 기본 정의열 (BuiltIn) — VFS 메타
이름 · **확장자**(분리 옵션) · 크기 · 종류 · 생성/수정/접근일 · 속성(읽기전용/숨김/시스템) · **클라우드 상태** · 소유자 · 전체 경로.
- **이름/확장자 분리 옵션**: 단일 `이름` ↔ `이름`+`확장자` 2열 토글(설정).
- **클라우드 상태 열**: 로컬 / 온라인(클라우드만) / 동기중 / 오류 — 아이콘+툴팁. VFS provider가 sync 상태 제공(FR-G).

### 2-2. 사용자 정의열 (Computed) — 수식/문자열 조합
- 간단한 **표현식**으로 셀 값을 계산. 변수 = 다른 컬럼/메타 값.
- 예: `=size/1024 & " KB"` · `=upper(name)` · `=if(kind="dir","폴더","파일")` · `={modified:yyyy-MM}` · `=name & " (" & ext & ")"`.
- **안전 샌드박스**: 순수 계산만(파일 I/O·네트워크·부작용 **금지**), 화이트리스트 함수(문자열/산술/날짜/조건). 무한루프 불가(비재귀 식).
- 평가 위치: 다량 행 → **코어(Rust) `FileSource`에서 평가** 우선(성능), 단순식은 C# 폴백. 가상화로 **보이는 셀만** 평가.

### 2-3. 플러그인열 (Plugin)
해시(MD5/SHA)·EXIF·이미지 해상도·미디어 길이 등 — 플러그인이 `ICellValueProvider` 제공([09](09-plugin-architecture.md)). 비동기 계산 + 캐시.

## 3. 듀얼 패널 컬럼 동기화 (정책 선택)
| 정책 | 동작 | 비고 |
| --- | --- | --- |
| **독립(Independent)** | 패널마다 `ColumnLayout` 따로 | 기본값 권장 |
| **실시간 동기(Live)** | 한쪽 너비/구성 변경 → 즉시 반대쪽 반영 | 양방향 |
| **수동 적용(Apply)** | 명령으로 동기: `좌→우` · `우→좌` · `양쪽 맞춤` | 의도적 |

- 동기 단위 = `ColumnLayout`(순서·너비·표시·정렬). `PaneHost`가 정책에 따라 레이아웃을 복사/공유.
- **레이아웃 프로파일**: 폴더 종류·경로 패턴별 다른 컬럼 세트 저장·자동 적용(예: 이미지 폴더 = 해상도 열). 세션 복원.

## 4. 정렬·그룹 (컬럼 연계)
- 컬럼 헤더 클릭 = 정렬(셀의 **정렬키**로, 표시 텍스트와 별개 — 크기/날짜 올바른 정렬). 다중 키 정렬(P1).
- 그룹화(종류/날짜 등)도 컬럼 값 기반(후속).

## 5. 컴포넌트 요구 (구조가 지원해야 할 것)
- 컬럼 추가/제거/재정렬/리사이즈/표시토글 **런타임** + 직렬화.
- 셀 값은 `ICellValueProvider`로 위임(기본/수식/플러그인 동일 취급).
- 헤더 = 별도 행(컬럼 너비를 본문과 공유), 리사이즈(CommunityToolkit Sizers).
- 보이는 셀만 평가(가상화) + 비동기 셀(플러그인/수식) 자리표시자→갱신.

## 6. 영향
- 코어(Rust): `FileSource`가 메타 + **수식 평가**(샌드박스) 제공, sync 상태 메타.
- 라이브러리(`Nexa.Controls`): `IColumn`/`ICellValueProvider`/`ColumnLayout` 1급.
- 앱: 컬럼 관리 UI(추가/숨김/수식 편집), 동기화 정책 설정, 프로파일.
- 요구: [05](05-requirements.md) FR-A8.
