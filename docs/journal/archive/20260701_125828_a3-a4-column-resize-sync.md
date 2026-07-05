# 작업 기록 — 2026-07-01 12:58:28 (KST)

> 기록 ID: `20260701_125828_a3-a4-column-resize-sync`
> 이전 기록: `20260701_120301_a2-column-header`

## 1. 요청
- 순서대로 진행 — **A3**(컬럼 리사이즈). 설계상 **A4**(좌/우 동기화·요구4)도 함께 달성.

## 2. 구현 (A3 + A4)
- **공유 컬럼 방식**: `NexaGridColumn.Width`를 **INotifyPropertyChanged**로. **하나의 컬럼 인스턴스를 헤더·본문·좌/우 패널이 공유**하면 Width 변경이 모두에 반영.
  - 컬럼을 `RootGrid.Resources`(`ColKind`/`ColName`/`ColSize`)로 정의, 코드에서 좌/우 `NexaFileGrid.Columns`에 **같은 인스턴스** 추가.
  - 헤더 Border `Width="{x:Bind Width, OneWay}"`, 본문 셀 `Width="{Binding Source={StaticResource ColX}, Path=Width, OneWay}"`.
- **리사이즈 핸들**(NexaFileGrid.xaml): 헤더 각 컬럼 우측 6px `Border`, `ManipulationMode=TranslateX` + `ManipulationDelta="OnColumnResize"` → `col.Width = max(40, +ΔX)`.
- **결과**: 헤더 드래그 → 헤더+본문+**좌/우 양쪽** 동시 리사이즈(A4 동기화).

## 3. 빌드 이슈 2건 & 원인 (교훈)
1. **WinUI 3 `Window`은 `Resources` 미지원**(FrameworkElement 아님) → `<Window.Resources>`가 XamlCompiler 실패(MSB3073). **`Grid.Resources`로 이동** 해결.
2. **`IList` 컬렉션에 `<StaticResource>` 요소로 항목 추가 불가** → XamlCompiler 실패. **코드 비하인드에서 `Columns.Add(Resources[key])`**.
- (진단: 라이브러리 단독 빌드는 성공 → 앱 XAML이 원인으로 좁힘. XamlCompiler는 상세 메시지를 콘솔에 안 남겨 이분화로 특정.)

## 4. 검증
- `dotnet build app/Nexa.App -c Debug` → **0/0**. Nexa.App.exe 기동 정상.
- 육안: `dotnet run` → 헤더 경계 드래그로 컬럼 너비 조절, 본문·좌/우 패널 동시 반영.

## 5. 다음 (A5)
- 정렬(헤더 클릭, 표시 텍스트와 별개 정렬키). 이후 트랙 B(네비게이션).
- A4 정책(독립/수동 적용)은 후속 — 현재는 공유=실시간 동기화 기본.
