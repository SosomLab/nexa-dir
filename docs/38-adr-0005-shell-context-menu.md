# 38 · ADR-0005 — 셸 컨텍스트 메뉴 통합 (클래식 네이티브 호스팅 + 고유 항목 병합, B-2)

- 상태: **Accepted** (2026-07-07) · 관련: [33 파일조작·DnD](33-file-ops-dnd-design.md)·[10 결정](10-decision-record.md)·[TODO B-2](TODO.md)·[08 경쟁조사](08-competitive-feature-survey.md)
- 대상 단위: **B-2**(TODO §2, M1 P0 — "뷰어→탐색기" 제품 정체성).

## 맥락 (문제)

현재 컨텍스트 메뉴는 **고유 XAML `MenuFlyout`** 뿐(열기·잘라내기/복사/붙여넣기·삭제·이름변경·새로만들기·새로고침, B-10c).
탐색기 대체가 되려면 **셸 확장 생태계**(7-Zip·Git·TortoiseSVN·"보내기"·"열기 방법"·"속성"·OneDrive 등)가 우클릭에 떠야 한다.

**플랫폼 제약**: Windows 11 신형(WinUI 스타일) 메뉴는 **호스팅 공개 API가 없다**(탐색기 전용). 서드파티 앱이 셸 메뉴를
호스팅하는 유일한 공개 경로는 **고전 `IContextMenu`(COM)** — 클래식(Win10/"더 많은 옵션 표시") 메뉴가 뜬다.
`IExplorerCommand`는 "우리 항목을 **탐색기** 메뉴에 추가"하는 반대 방향 API로, 호스팅과 무관(후속 별도).

## 결정 (사용자 확정 2026-07-07)

**우클릭 = 클래식 네이티브 셸 메뉴(HMENU) 하나로 통합하고, 그 안에 고유 항목을 병합한다.**

- 셸 항목: `IContextMenu.QueryContextMenu`가 HMENU에 채움 — **명령 ID 대역 1~0x7FFF**.
- 고유 항목: **ID 0x8000+** 로 같은 HMENU에 `AppendMenu`(구분자로 섹션 분리). 선택 ID가 셸 대역이면
  `InvokeCommand`, 고유 대역이면 기존 핸들러 호출. (클래식 파일매니저 표준 기법 — ID 대역 분리로 충돌 없음.)
- 시각: Win32 클래식 메뉴 룩(탐색기 "더 많은 옵션"과 동일). **다크 XAML 테마와의 이질감은 수용**(사용자 결정 —
  클래식 스타일 선호).

### 대안 비교 (비채택 근거)
| 안 | 내용 | 판정 |
|---|---|---|
| **A. 네이티브 HMENU + 병합** | 셸 충실도 100%(동적 서브메뉴·owner-draw 포함), 고유 항목 공존, 규모 소~중 | ★ **채택** |
| B. XAML 병합(항목 열거→MenuFlyout 변환) | 테마 일치하나 owner-draw·동적 서브메뉴("보내기"/"열기 방법") 재현 난제. Files 앱이 이 길로 갔고 비용 매우 큼(수 세션+) | 보류(후속 검토 여지) |
| C. Win11 신형 메뉴 호스팅 | 공개 API 없음 | 불가 |

## 설계

### 구성물: `ShellContextMenu.cs` (앱 계층, 수동 COM 인터롭 — ShellLink/OleDropTarget 기존 패턴)

```
Show(hwnd, paths, screenPt, customItems) → 선택 실행 여부
```

1. **항목 바인딩**: 경로별 `SHParseDisplayName` → full PIDL → `SHBindToParent` → 부모 `IShellFolder` + child PIDL.
   다중 선택은 **같은 부모 폴더 항목만** 묶어 `GetUIObjectOf(hwnd, n, childPidls, IID_IContextMenu)`
   (인라인 트리의 **교차 부모 선택**은 슬라이스 1에선 **클릭 항목 폴더 기준으로 축소** — 후속 확장).
2. **메뉴 구성**: `CreatePopupMenu` → `QueryContextMenu(hmenu, 0, 1, 0x7FFF, CMF_NORMAL|CMF_CANRENAME[|CMF_EXTENDEDVERBS←Shift])`
   → 하단에 구분자 + **고유 항목(0x8000+)** `AppendMenu`.
3. **동적 서브메뉴/owner-draw**: `IContextMenu2/3`로 QI 후, **최상위 HWND를 `SetWindowSubclass`(comctl32)** 하여
   `WM_INITMENUPOPUP`/`WM_MEASUREITEM`/`WM_DRAWITEM`/`WM_MENUCHAR`를 `HandleMenuMsg(2)`로 포워딩 — "보내기"·"열기 방법"
   지연 채움과 아이콘이 동작. 메뉴 종료 시 `RemoveWindowSubclass`.
4. **표시/선택**: `TrackPopupMenuEx(TPM_RETURNCMD|TPM_RIGHTBUTTON, GetCursorPos)` (모달 메뉴 루프 — Win32 표준,
   WinUI UI 스레드에서 동작). 반환 ID 분기:
   - 1~0x7FFF → `InvokeCommand(CMINVOKECOMMANDINFOEX{ lpVerb=MAKEINTRESOURCE(id-1), CMIC_MASK_UNICODE|PTINVOKE })`.
   - 0x8000+ → 호출자가 넘긴 고유 항목 콜백.
5. **정리**: `DestroyMenu` + COM 해제(`FinalReleaseComObject`) + PIDL `ILFree`. 전 구간 try/finally.

### 고유 병합 항목 (슬라이스 1)
셸이 이미 제공하는 것(열기/잘라내기/복사/삭제/이름 바꾸기/속성 등)은 **중복 추가하지 않는다**. 우리 고유:
- **완전 삭제(Shift+Del)** — `DeletePaths(permanent:true)`
- **폴더에 붙여넣기**(폴더 항목 + 클립보드 있을 때) — `PasteIntoDir`
(후속 슬라이스에서 "경로 복사" 등 프로툴 항목 추가 여지.)

### 슬라이스 계획
- **S1**: 항목(행) 우클릭 = 셸 메뉴 + 고유 병합. 기존 행 XAML 메뉴 대체.
- **S2**: 빈 영역 = 폴더 **배경** 셸 메뉴(`IShellFolder.CreateViewObject(IID_IContextMenu)`) + "새로 만들기" 셸 서브메뉴 —
  기존 빈영역 XAML 메뉴 대체.
- **S3**: 폴리시 — Shift=확장 동사(CMF_EXTENDEDVERBS), 기본 동사 굵게(SetMenuDefaultItem), 교차 부모 선택 처리.

## 위험과 수용/완화

1. **in-proc 셸 확장 로드**: 서드파티 DLL이 **우리 프로세스에 로드**됨 — 확장 크래시=앱 크래시(탐색기 동급 리스크).
   NFR "오류 격리"와 상충하나 **수용**(모든 클래식 파일매니저 동일 트레이드오프). 완화 후속: 예외 가드, 문제 확장
   블랙리스트 옵션, (원거리) 브로커 프로세스 분리.
2. **첫 호출 지연**: 확장 DLL 로드로 첫 우클릭이 수백 ms 걸릴 수 있음(탐색기도 동일). 우클릭 시에만 lazy 생성.
3. **STA/메시지 루프**: `TrackPopupMenuEx`는 자체 모달 펌프 — WinUI 스레드에서 표준 동작. 서브클래스는 메뉴 표시
   구간에만 설치.
4. **상승(High IL) 프로세스**(이 PC UAC OFF): 우리가 호출 주체라 **드래그(BUG-009)와 달리 제약 없음**.
5. **unpackaged**: `IContextMenu` 경로는 패키지 정체성 불요 — 현 배포 형태 그대로 동작.

## 결과 (기대)

- 우클릭이 탐색기(클래식)와 동일한 항목 + Nexa 고유 항목 → B-2(M1 P0) 핵심 충족.
- XAML 메뉴 코드는 S1(행)·S2(빈영역)에서 순차 대체 — 진입점·액션 핸들러(`ContextTargets`·`DeletePaths` 등)는 재사용.
