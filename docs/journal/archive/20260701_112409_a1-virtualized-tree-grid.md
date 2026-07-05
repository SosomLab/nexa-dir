# 작업 기록 — 2026-07-01 11:24:09 (KST)

> 기록 ID: `20260701_112409_a1-virtualized-tree-grid`
> 이전 기록: `20260701_110915_dual-panel-list-and-dock-link`

## 1. 요청
- 트랙 A(기반·재사용) 진행 — 기능 단위 세분화 + 단계별. **A1**: `Nexa.Controls` + `VirtualizedTreeGrid` 추출(좌/우 목록 중복 제거).

## 2. A 트랙 세분화 (각 단위=커밋)
- **A1** VirtualizedTreeGrid 추출 ← 이번
- A2 컬럼 헤더 + 공유 컬럼 모델(IColumn/ICellValueProvider)
- A3 컬럼 리사이즈(헤더 드래그)
- A4 컬럼 크기 동기화(좌/우, 독립/실시간/수동 — 요구4)
- A5 정렬(헤더 클릭, 정렬키)

## 3. 구현 (A1)
- **`app/Nexa.Controls`**(WinUI 클래스 라이브러리, net8.0-windows10.0.22621.0): `VirtualizedTreeGrid`(UserControl) — `ScrollViewer`+`ItemsRepeater` 래핑, `ItemsSource`/`ItemTemplate` DP. **도메인 비종속**(행 의미 모름, ADR-0002 §9).
- **Nexa.App**: `ProjectReference` 추가. 좌/우 패널의 `ScrollViewer`+`ItemsRepeater` 보일러플레이트 → `<ctrls:VirtualizedTreeGrid>` 로 교체(`DirGrid`/`DirGrid2`). `LoadDirectory(path, grid, header, path)` 시그니처.

## 4. 빌드 이슈 3건 & 해결
1. **MSB4062**(ExpandPriContent 태스크 로드 실패, 라이브러리): WinUI 라이브러리에 `<EnableMsixTooling>true</EnableMsixTooling>` 추가(앱과 동일).
2. **MSB3073 XamlCompiler exit 1**: **리소스 `DataTemplate` + `x:Bind` + DP 주입** 조합이 XamlCompiler 실패 → 각 컨트롤에 **인라인 `x:Bind` 템플릿**으로 해결(검증된 방식).
3. (기존) 실행 중 앱이 exe 잠금 → `Get-Process Nexa.App | Stop-Process` 후 재빌드.

## 5. 검증
- `dotnet build app/Nexa.App -c Debug` → **0 Warning / 0 Error**(라이브러리+앱 동시 빌드).
- Nexa.App.exe 기동 → 좌/우 `VirtualizedTreeGrid` 목록 정상.

## 6. 다음 (A2)
- `VirtualizedTreeGrid`에 **컬럼 헤더 + 공유 컬럼 모델**(`IColumn`/`ICellValueProvider`) 도입 → 이후 A3 리사이즈 → A4 동기화(요구4).
- 교훈: WinUI **리소스 DataTemplate에 `x:Bind`+DP 주입 조합은 XamlCompiler가 실패** → 인라인 템플릿 또는 `{Binding}`.
