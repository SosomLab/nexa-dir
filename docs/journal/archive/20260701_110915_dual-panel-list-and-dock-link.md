# 작업 기록 — 2026-07-01 11:09:15 (KST)

> 기록 ID: `20260701_110915_dual-panel-list-and-dock-link`
> 이전 기록: `20260701_105652_fix-sizers-tfm`

## 1. 요청 (사용자, 레이아웃 보완 4건)
1. 하단 도킹은 **듀얼 패널일 때만** 좌/우 분리 가능. 우/좌 패널 숨기거나 1단 구성(좌 마스터) 시, 하단 분리돼 있으면 **해당 방향 정보 패널도 숨김**.
2. 상단 Top-down 메뉴(파일/선택 등) **세로 크기 Fit**.
3. **우 패널도 좌 패널처럼 파일 목록** 표시.
4. 파일 정보 **컬럼 크기 조절 시 양쪽 패널 동기화**(기본) — "진행할꺼야".

## 2. 구현 (1~3)
- **우 패널 파일 목록**(요구 3): 우 패널 placeholder → 좌 패널과 동일 구조(탭바·네비바·헤더·`ItemsRepeater`). `DirRepeater2`/`DirHeader2`/`PathText2`. `LoadDirectory(path, repeater, header, pathText)`로 파라미터화해 좌(홈)·우(문서) 각각 로드.
- **하단 도킹 듀얼 연동**(요구 1): `UpdateBottomDock()` 신설 — 하단 우 도킹 표시 = **우 패널 표시(듀얼) AND 하단 분리 ON**. 우 패널 숨기면 하단 우 숨김 + "분리" 토글 `IsEnabled=false`. `OnToggleRightPanel`/`OnToggleTerminal`/`OnToggleBottomSplit`이 이 메서드로 수렴.
- **메뉴 세로 Fit**(요구 2): `MenuBar Height=28 VerticalAlignment=Top`.

## 3. 미구현 (4 — 다음 단위)
- **컬럼 크기 동기화**: 현재 목록은 컬럼 헤더/리사이즈가 없음(행 템플릿 고정 3열). 컬럼 헤더+리사이즈(ADR-0002 §7-2) 도입이 선행 → 그 위에 [23](23-column-system.md) §3(듀얼 동기화 정책)로 구현. docs/20 §5에 "컬럼 동기화(예정)" 명시.

## 4. 검증
- `dotnet build app/Nexa.App -c Debug` → **0 Warning / 0 Error**(실행 중 앱 잠금은 프로세스 종료 후 재빌드).
- Nexa.App.exe 기동 → 좌/우 패널 목록·하단 연동·메뉴 Fit 예외 없이 동작.
- ※ 빌드 전 실행 중 앱이 exe를 잠그면 MSB3027 → `Get-Process Nexa.App | Stop-Process` 후 재빌드(§docs/18 dll 잠금과 동류).

## 5. 다음
- 컬럼 시스템(헤더·리사이즈·동기화) → 네비게이션(폴더 진입·뒤로/앞으로) → 좌/우 패널을 재사용 컴포넌트(`Nexa.Controls` VirtualizedTreeGrid)로 추출.
