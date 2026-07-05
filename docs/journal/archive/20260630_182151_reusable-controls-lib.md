# 작업 기록 — 2026-06-30 18:21:51 (KST)

> 기록 ID: `20260630_182151_reusable-controls-lib`
> 이전 기록: `20260630_181512_adr0003-modules`

## 1. 요청 (사용자)
- ItemsRepeater 기반 컴포넌트를 **라이브러리 방식**으로 분리해 **다른 기능 구현에서도 가져다 사용**할 수 있게 설계/개발.

## 2. 결정 (ADR-0002 §9 보강, [21](21-adr-0002-fileview-control.md))
- **별도 WinUI 클래스 라이브러리 `app/Nexa.Controls`** 신설(예정).
- **도메인 비종속 컨트롤** `VirtualizedTreeGrid`: 행/컬럼/선택을 추상 인터페이스(`IRowSource`/`IColumn`/`ISelectionModel`)로 받음 — 파일 개념 모름.
- **파일 특화 = 어댑터** `FileTreeGrid` = `VirtualizedTreeGrid` + 파일 `RowSource`(코어 `FileSource` 바인딩).
- 의존 경계: 컨트롤은 ItemsRepeater + CommunityToolkit Sizers만(코어/도메인 비참조) → **NuGet 패키지화 가능**.
- 재사용처: 파일 목록·검색 결과·클라우드 브라우저·플러그인 패널.
- 테스트: 가짜 `IRowSource`로 컨트롤 단위 검증(도메인 불필요).

## 3. 계층
컨트롤(표현/상호작용, Nexa.Controls) ↔ 어댑터(도메인 매핑, 앱) ↔ 코어(데이터, Rust).
ADR-0003 `IFileView`의 `DetailsView`가 `VirtualizedTreeGrid`를 사용.

## 4. 문서 현행화
- ADR-0002 §9(재사용 라이브러리) 추가.
- 구조 [16](16-project-structure.md): `app/Nexa.Controls/`(예정) 추가.

## 5. 다음
- 컨트롤 첫 구현 단위에서 `Nexa.Controls`(net8.0-windows, WinUI) 프로젝트 스캐폴딩 + 앱 참조(빌드 검증) → `VirtualizedTreeGrid` 골격.
