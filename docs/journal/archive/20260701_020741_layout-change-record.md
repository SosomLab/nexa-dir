# 작업 기록 — 2026-07-01 02:07:41 (KST) · 레이아웃 골격 변경 내역

> 기록 ID: `20260701_020741_layout-change-record`
> 이전 기록: `20260701_000906_macos-netsdk1100`
> 대상: 레이아웃 골격 3커밋(`105d9e8` → `7b982b9` → `597e52e`). **WinUI라 맥 미빌드 — Windows/VM 검증.**

## 1. 커밋별 요약 & 파일 줄수

### `105d9e8` feat(app/layout): 레이아웃 골격 — 영역 표시·크기 조절·숨김 토글 (F6)
| 파일 | 변경 |
| --- | --- |
| app/Nexa.App/MainWindow.xaml | +186 / -? (186 insertions, 일부 치환) |
| app/Nexa.App/MainWindow.xaml.cs | +30 |
| app/Nexa.App/Nexa.App.csproj | +2 (CommunityToolkit Sizers) |
| THIRD-PARTY-NOTICES.md | +3 / -1 |
| docs/19-implemented-features.md | +18 |
| **합계** | **5 files, +206 / -33** |

### `7b982b9` feat(app/layout): 좌/우 패널 기본 동일 크기(50:50)
| 파일 | 변경 |
| --- | --- |
| app/Nexa.App/MainWindow.xaml | +5 / -2 |
| app/Nexa.App/MainWindow.xaml.cs | +3 / -1 |
| **합계** | **2 files, +5 / -3** |

### `597e52e` feat(app/layout): 하단 도킹 좌/우 분리(기본) + 영역 색상 구분 초안
| 파일 | 변경 |
| --- | --- |
| app/Nexa.App/MainWindow.xaml | +121 (98 net, 53 치환) |
| app/Nexa.App/MainWindow.xaml.cs | +12 / -? |
| docs/20-ui-layout.md | +8 / -5 |
| docs/19-implemented-features.md | +5 |
| **합계** | **4 files, +98 / -53** |

## 2. 합산 (105d9e8^ → 597e52e) — numstat (추가 / 삭제)

| 파일 | 추가 | 삭제 |
| --- | ---: | ---: |
| app/Nexa.App/MainWindow.xaml | 187 | 31 |
| app/Nexa.App/MainWindow.xaml.cs | 40 | 1 |
| app/Nexa.App/Nexa.App.csproj | 2 | 0 |
| THIRD-PARTY-NOTICES.md | 2 | 1 |
| docs/19-implemented-features.md | 19 | 0 |
| docs/20-ui-layout.md | 8 | 5 |
| **총계** | **258** | **38** |

→ **6 files changed, 258 insertions(+), 38 deletions(-)**

## 3. 기능 변경 내용
- **F6 레이아웃 골격**: 7행 그리드(메뉴·도구모음·런처·메인(좌/우 듀얼)·하단 도킹·상태바),
  `ctk:GridSplitter`로 좌↔우·메인↔하단·하단 좌↔우 크기 조절, 상태바 토글(런처/우패널/하단/하단분리).
- **좌/우 패널 50:50**: RightCol 360px → star.
- **하단 도킹 좌/우 분리(기본)**: 좌(청록)/우(자주) 2개 — 터미널 활성 시 좌·우 2개.
- **영역 색상 구분 초안**: 메뉴 주황·도구 파랑·런처 초록·좌 파랑·우 빨강·하단 좌 청록·하단 우 자주·상태바 회색.
- 의존성: `CommunityToolkit.WinUI.Controls.Sizers`(MIT) 추가 → NOTICES 반영.
- 기존 보존: 좌 패널에 디렉터리 목록(F4/F5) 유지(DirRepeater/DirHeader/PathText).

## 4. 다음
- Windows/VM에서 레이아웃 확인 → 영역별 채움(폴더 진입·경로 바·탭 모델) 작은 단위 진행.
