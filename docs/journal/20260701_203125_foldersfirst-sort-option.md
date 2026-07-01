# 작업 기록 — 2026-07-01 20:31 (KST)

> 기록 ID: `20260701_203125_foldersfirst-sort-option`
> 이전 기록: `20260701_194726_nav-selection-menu-ux`

## 1. 요청
- 폴더 우선 정렬이 **기본값**이 되도록 반영하고, **나중에 설정에서 선택 가능**하도록 개발.

## 2. 판단
- 폴더 우선 정렬은 이미 `NativeInterop.ReadDir`에 **하드코딩**돼 동작 중(기본값은 사실상 충족).
- 핵심은 이를 **설정 값으로 분리**해 나중에 토글 가능한 구조로 만드는 것 → 이번 단위 = **설정 구조 초안**.
- 메뉴 명령 배선(NexaMenuEntry Command/Click)은 아직 없음(트랙 D) → 설정 UI 토글은 확장 단위로 분리.

## 3. 구현 (초안)
- **[Settings.cs](../../app/Nexa.App/Settings.cs) 신설**: `SortOptions { FoldersFirst = true }`(기본 폴더 우선) + `AppSettings.Sort` 인메모리 싱글턴. JSON 로드/저장 자리(주석) 표시.
- **[NativeInterop.cs](../../app/Nexa.App/NativeInterop.cs)**: `ReadDir(path, depth, SortOptions? sort=null)` — 생략 시 `AppSettings.Sort` 참조. 정렬 로직을 `SortItems(items, sort)` 헬퍼로 추출 → `FoldersFirst`가 켜져 있으면 폴더 우선, 아니면 이름만으로 정렬.
- 동작은 기존과 동일(기본 폴더 우선 + 이름 오름차순). 초기 로드·폴더 펼침 두 경로 모두 동일 옵션 적용.

## 4. 검증
- `dotnet build app/Nexa.App -c Debug` → **0/0**(실행 중 앱 DLL 락으로 1회 실패 → 앱 종료 후 재빌드 성공).
- 코어 변경 없음(앱 계층 정렬).

## 5. 다음(백로그)
- **설정 시스템(JSON)**: `AppSettings` 로드/저장(System.Text.Json), 변경 시 저장·실행 시 로드.
- **표시(보기) 메뉴 토글**: `NexaMenuEntry` 체크/명령 확장(트랙 D) → "폴더 우선" 체크 항목 배선 + 변경 시 열린 목록 **재정렬**.
- 정렬 키/방향(A5): 헤더 클릭 정렬과 통합(`SortOptions`에 SortKey·Descending 추가).
