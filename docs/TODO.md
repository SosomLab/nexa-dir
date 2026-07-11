# TODO — Nexa Dir 할 일 백로그 (범위 산정 · living)

> 3차 감사([journal/archive/20260703_200457_refactor-003-audit.md](journal/archive/20260703_200457_refactor-003-audit.md)) 기준 확정 백로그. **범위를 산정**하고, 새 항목은 **맨 하단 "§9 추가 항목"에 append**한다.
> 진행은 **사용자 지시 항목만**. 착수 시 해당 라인 상태를 갱신하고, 완료 시 커밋 해시를 단다.
>
> **규모**: 소=반나절/1커밋 · 중=1~2일/1~3슬라이스 · 대=3일+/다수 슬라이스·설계(ADR) 동반.
> **상태**: ☐ 대기 · 🚧 진행 · ✅ 완료(커밋) · ⏸ 보류.
> **우선**: P0 최우선(M1 필수·제품 정체성) · P1 높음 · P2 중간 · P3 낮음 · 측정.

---

## §1. 트랙 A — 성능·메모리 보증 (한 성능 작업을 "실보장"으로)

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| A-1 | `_cache` 뷰포트 LRU 상한 + 선택/포커스 갱신을 realized 행에만 (O(방문행수) 열화 제거) | P1 | 중 | — | ✅ (07-10 감사004 `a9c5682` — 행 재활용 이빅션 방식, 실화면 한정) |
| A-2 | 다중 탭 총량 상한(비활성 탭 핸들 Close/캐시 비움, 스냅샷 보존) | P1 | 중 | A-1 | ☐ |
| A-3 | 코어 arena 노드 회수(collapse 시 free-list/`loaded=false`) — P5 | P2 | 중 | — | ☐ |
| A-4 | 유휴/최소화 트림 자니터(`EmptyWorkingSet`·LRU 축소·압박 구독) NFR-M2/3/4 | P2 | 중 | A-1,A-2 | 🚧 힙 적응은 **DATAS ✅**(감사004) · 자니터 잔여 |
| A-5 | 아이콘 큐 처리율(즉시 착수·완료 즉시 리필) | P2 | 소 | — | ☐ |
| A-6 | `ScrollIndexIntoView` 익스텐트 선계산으로 `UpdateLayout` 반복 제거 · `Count` 캐시 | P3 | 소 | — | 🚧 Count 캐시 ✅(감사004 `a9c5682`) · 익스텐트 선계산 잔여 |
| A-7 | 실측: soak RSS·콜드스타트·60fps·대형 펼침 Add 비용 (측정 하네스) | 측정 | 중 | — | ☐ |

## §2. 트랙 B — 핵심 기능 (M1 P0 · 제품 정체성: "뷰어→탐색기") ★

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| B-1 | `nexa-ops` 파일 작업 엔진(복사/이동/삭제/이름변경/새폴더) **+ 에러/취소/진행률/Undo/휴지통/충돌 표준 동반** | P0 | 대 | C-1 | ☐ |
| B-2 | 컨텍스트 메뉴(셸 `IContextMenu` 호스팅 + 고유 항목) — [ADR-0005](38-adr-0005-shell-context-menu.md) | P0 | 대 | — | 🚧 S1 완료(행 메뉴 = 클래식 셸+고유 병합·커스텀 레지스트리·Checksum·**경로복사 제자리대체** §7-5) · S2 빈영역·S3 폴리시 남음 |
| B-3 | 클립보드(복사/잘라내기/붙여넣기, 셸 상호운용) | P0 | 중 | B-1(부분) | ☐ |
| B-4 | 드래그앤드롭(패널간·경로바) + 러버밴드 선택 + Ctrl+A/Home/End/PageUp | P1 | 대 | — | 🚧 **러버밴드 ✅(07-08)** — 미선택 행/빈 공간 드래그=밴드·행 히트영역=컬럼 총폭. 잔여: 경로바 드롭(B-17dnd)·Ctrl+A/Home/End/PageUp 확인 |
| B-5 | 교차폴더 다중 선택 완성(C3/C4) — 플래그십 | P1 | 중~대 | — | ☐ |
| B-6 | 인라인 이름변경(선택 후 재클릭/F2) | P2 | 소~중 | B-1 | 🚧 1차 구현(직접 IO·컴팩트 편집기). Undo/배치/ops 통합·컨트롤 승격은 후속 |
| B-7 | 사이드바(즐겨찾기/드라이브/네비트리/최근) | P2 | 중~대 | — | ☐ |

## §3. 트랙 C — 설계 계약 동결 (M1 마감 전 · M2+ 관문)

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| C-1 | 에러 모델 표준(코어 enum + last-error) — ops/preview/vfs 전제 | P0 | 중 | — | ☐ |
| C-2 | VFS Provider 계약(list/stat/read/watch) + 트리 Provider 경유 — M4/M6 관문 | P1 | 중~대 | — | ☐ |
| C-3 | watcher 설계(무효화·선택 pruning 훅) + NodeId 안정성 계약 | P1 | 중 | C-2 | ☐ |
| C-4 | docs/01 아키텍처 현행화(nexa-tree 반영·현존/예정 구분) | P2 | 소 | — | ☐ |

## §4. 트랙 D — 인프라·영속·접근성 (상용/공개 전환 대비)

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| D-1 | 설정 영속화(JSON) + 세션/창 복원(docs/28) — 재시작 소실 해소 | P1 | 중~대 | — | 🚧 설정·세션 ✅(PREF-1·SESS) · **창 위치/크기 복원(docs/28) 잔여** |
| D-2 | i18n 인프라(.resw/ResourceLoader) — 문자열 쌓이기 전 지금이 최저비용 | P1 | 중 | — | ✅ (방식 변경: 외부 `.lang`+Localizer, PREF-8 — 07-08~10) |
| D-3 | 접근성(AutomationProperties/UIA Peer) — 커스텀 컨트롤 늘기 전 배선 | P1 | 중~대 | — | ☐ |
| D-4 | NFR-M2/P5 실측 재보정 · 배포 이원화(MSIX+포터블) · 상주 규율 (2차 D 이월) | P2 | 중 | A-7 | ☐ |

## §5. 트랙 E — 구조 리팩터링·퀵윈 (저위험 · 지금이 최저비용)

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| E-1 | 죽은 코드 제거(`nexa_dir_*` 본체·`RestoreVerticalOffset`/`VerticalOffset`·`SortOptions`) | P2 | 소 | — | ☐ |
| E-2 | 순수 로직 추출(ApplyDiff 시프트·ParentIndex·SnapTarget·오프셋계산 → ViewModels 테스트) | P2 | 소 | — | ☐ |
| E-3 | `PanelControl` UserControl로 XAML 좌/우 dedup (2차 B-3) | P2 | 대 | — | ☐ |
| E-4 | `OnGridKeyDown` → 명령 레지스트리 + `keybindings.json`(FR-I2) | P2 | 대 | — | ☐ |
| E-5 | csbindgen 도입(수동 P/Invoke 미러 대체) | P2 | 중 | — | ☐ |
| E-6 | interop 에러코드 `#[repr(i32)] NexaStatus` 통일 (C-1과 연동) | P2 | 중 | C-1 | ☐ |
| E-7 | `DirItem`/`NexaFileKind` 파일 분리 + `Directory.Build.props` | P3 | 소 | — | ☐ |
| E-8 | `UpdateNavButtons` 좌/우 분기 소거(바인딩화) | P3 | 소 | — | ☐ |
| E-9 | (2차 이월) `panic="abort"`(FFI 언와인딩 가드) | P3 | 소 | — | 🚧 선택 id 경계 가드 ✅(감사004 `d17cf6c`) · panic=abort/catch_unwind 잔여 |
| E-10 | (2차 이월) 심링크 `symlink_metadata` + attrs 유닛테스트 | P3 | 소 | — | ☐ |
| E-11 | (2차 이월) docs/19 커밋해시 백필(플레이스홀더 21개) | P3 | 중 | — | ☐ |

## §6. 별도 (QA/모니터링)

| ID | 항목 | 우선 | 규모 | 상태 |
|---|---|---|---|---|
| BUG-001 | 빈 폴더 이탈 시 목록 blank ([BUGS.md](BUGS.md)) | — | — | ✅ 해결(2026-07-03 QA) |
| UX-1 | 탭 복귀 시 스크롤/선택 유지(활성 탭 재클릭은 맨 위) | P2 | 소~중 | ☐ |
| QA-1 | 트랙 A/B 회귀 실기 QA(뒤/앞/위·탭·펼침·아이콘·스크롤) | — | 소 | ☐ |

---

## §7. 권고 착수 순서 (범위·의존 반영)

1. **[관문 먼저]** C-1 에러 모델(중) → B-1 ops(대)의 전제. B-1 착수 시 C-1을 함께 동결(재작업 방지).
2. **[제품 정체성]** B-1 ops → B-3 클립보드 → B-2 컨텍스트 메뉴 → B-4 DnD → B-5 교차선택 완성.
3. **[성능 실보장]** A-1 `_cache` 상한(B와 병행 가능, 큰 목록 기능 얹기 전 필수) → A-2 다중탭 캡.
4. **[저비용 병행]** E-1 죽은코드·E-2 순수추출·D-2 i18n은 코드/문자열 쌓이기 전 틈틈이(큰 기능과 별개).
5. **[M2 전]** C-2 Provider·C-3 watcher는 M1 마감·검색/미리보기 착수 전 동결.

## §8. 하지 말 것 (범위 경계)
- watcher/preview/search를 **ops·Provider·에러 모델 계약 동결 전**에 얹기.
- `_cache` 상한(A-1) 없이 새 대형 목록 기능 추가.
- 미사용 선택 API·과도한 추상화를 실사용(C3/ops) 전에 확장.

---

## §9. 추가 항목 (append-only)

> 새로 식별된 할 일은 여기에 추가한다. ID는 트랙 접두사 + 다음 번호(예: A-8, B-8, E-12).
> **시간 기록(추가/설계/개발/테스트/완료 일시)은 [TASKS.md 작업 원장](TASKS.md)에서 단일 추적.** 아래는 요약 인덱스.

### §9-1. 2026-07-04 세션 요청 (파일 조작 UX 심화 · 버그 · 컬럼) → 상세·일시는 [TASKS.md](TASKS.md)

- **완료(이번 세션)**: 더블클릭 실행(ShellExecute, BUG-003)·새 탭 이름(BUG-005)·비활성 패널 클릭 활성화(BUG-004)·휴지통 삭제 메서드.
- **마우스 재정비(세트)**: `B-8` 이름변경 release+우클릭 제외(BUG-002) · `B-9m` 다중선택 드래그(N개) + 선택 보존.
- **파일 조작**: `B-11r` 휴지통 삭제 배선(Del=휴지통/Shift+Del=완전) · `B-10c` 컨텍스트 메뉴 폴더/파일/빈영역 분리.
- **DnD 정책(설계)**: `B-14dnd` 디스크별 기본(같은=이동/다른=복사)+Alt 반전 · `B-15h` 폴더 3초 hover 진입 + 전환시간 설정(탭2초/폴더3초).
- **자동 갱신·되돌리기(설계)**: `B-12w` watcher Pub/Sub + 수동 새로고침(F5)[BUG-006] · `B-13u` Undo/Redo(Ctrl+Z/Y)+히스토리 클래스 — **✅ S1+S2 완료(07-07)**: `OperationHistory`+이동/복사/이름변경/새로만들기/**휴지통 삭제 복원**(셸 undelete) undo·redo(xUnit 10). 잔여: nexa-ops 이관(B-1)·다중 삭제본 시각 비교.
- **컬럼**([docs/23](23-column-system.md)): `COL-1` 확장자 컬럼 기본표시 · `COL-2` 정렬 3상태(오름→내림→없음)+헤더 화살표 · `COL-3` Alt+헤더 다중정렬 · `COL-4` 컬럼 조정 모달.
- **셸 통합**: `SHELL` = 기존 §2 **B-2**(`IContextMenu` 호스팅, 별도 ADR 후보).

### §9-2. 2026-07-07 세션 — DnD 개선 후속 (🔴 긴급)

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| B-16dnd | [BUG-009](BUGS.md) **외부(탐색기→앱) 드래그 금지 커서** — 원인: UAC OFF PC는 항상 상승 실행 + WinUI 3가 상승 프로세스 인바운드 드래그 거부(플랫폼 제한, microsoft-ui-xaml#7690/#10119). 해결: 상승 감지 시 고전 OLE `IDropTarget` 폴백(`OleDropTarget.cs`) 등록. 진단 코드 제거 완료 | **P0** | 소~중 | — | ☑ 해결(07-07) |
| B-17dnd | 경로 바 세그먼트 드롭 타깃(탐색기 breadcrumb식) — [docs/33](33-file-ops-dnd-design.md) 07-07 절 잔여 | P3 | 소 | — | ☐ |

### §9-3. 2026-07-08 세션 — 설정(Preferences) 시스템 + 요청 기능 (설계 [40](40-preferences-system.md))

> **통합 설정 창**([40](40-preferences-system.md))이 다수 요청을 페이지로 수렴. 인프라(S1)가 선행 관문 —
> 지금 인메모리 옵션 4벌(Theme/Menu/View/Tab)이 재시작 소실 중이라 우선순위 높음.

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| PREF-1 | **설정 인프라** — `SettingsStore`(settings.json 로밍·마이그레이션) + 기존 그룹 영속 배선 + `Ctrl+,` 설정 창 뼈대([40](40-preferences-system.md) S1) | **P1** | 중 | — | ✅ (07-08 `d99c550` · 창 VS Code식 재구성 07-10 PR#9) |
| PREF-2 | **모양 페이지** — 테마 모드/팩(토큰 색)/폰트(UI·모노)/밀도([39 §5](39-theme-system.md)) | P1 | 중 | PREF-1 | 🚧 테마 모드·**글꼴 5종+밀도 ✅**(PR#9) · 테마팩(토큰 색) 잔여 |
| PREF-3 | **레이아웃 페이지** — 패널/런처/하단 표시·헤더·전송창 자동닫기 토글 이관 | P2 | 소 | PREF-1 | ✅ (07-10 PR#9 — 영속 설정 전부 설정 창 수록) |
| PREF-4 | **컬럼 설정**(COL-4) — 표시/순서/너비/기본 정렬·per-tab([23](23-column-system.md)) | P2 | 중 | PREF-1 | ☐ |
| PREF-5 | **단축키 지정** — 액션 레지스트리([26 §5](26-command-palette.md))+`keybindings.json`+충돌 검사 | P2 | 중~대 | PREF-1 | ☐ |
| PREF-6 | **퀵 런처 바 설정** — 등록 도구(경로/인자 템플릿/아이콘)·순서·실행 배선(placeholder→실기능) | P1 | 중 | PREF-1 | 🚧 슬라이스1 ✅(도구 모음 3종+런처 시드 VS Code·exe 아이콘·실행, [docs/44](44-toolbar-and-launcher.md)) · CRUD/영속·단축키(PREF-5) 후속 |
| PREF-7 | **즐겨찾기 관리** — 목록 추가/삭제/순서 + **사이드바**(B-7) 노출 | P2 | 중~대 | PREF-1 | ☐ |
| PREF-8 | **언어(i18n)** — 인프라(순수 Localizer+테이블+LocExtension)·언어 페이지·문화 영속·**외부 `.lang` 파일화**([42](42-i18n-language-files.md): 설치+사용자 폴더·포맷 심 JSON↔properties·기준 en) **✅(07-08)**. 잔여: 문자열 전면 이관·번역 언어 추가 | P1 | 중 | — | 🚧 인프라·메뉴바·설정창·외부파일 ✅ |
| ARCH-1 | **압축 파일 지원(내장 zip/아카이브)** — 가상 폴더 탐색(VFS Provider C-2) + 압축/해제(전송 엔진). 별도 ADR | P2 | 대 | C-2 | ☐ 설계 |
| SRCH-1 | **파일 찾기(Everything식)** = M3([24](24-search-everything.md)) — MFT/USN 인덱스·필터 문법 | P2 | 대 | — | ☐ M3 |
| REN-1 | **일괄 이름 변경 α** — 동작 6종(치환/정규식/삽입/대소문자/연번/날짜)+빌딩 블록(순서)+실시간 미리보기+충돌 검출+적용/Undo. `nexa-rename`(순수·맥 테스트). 진입=명령/컨텍스트/팔레트([25](25-bulk-rename.md)) | P1 | 대 | — | ☐ 설계(2026-07-08 스펙 확장) |
| REN-2 | **리네임 표현식/함수 언어 β** — 토큰+문자열 함수(LEFT/SUBSTR/BETWEEN/PAD/PROPER/IF…)+널 처리(IFNULL/COALESCE/ISBLANK, 빈값 정책)+프리셋 저장/재사용([25 §4](25-bulk-rename.md)) | P2 | 중~대 | REN-1 | ☐ 설계 |
| META-1 | **`nexa-meta` 메타데이터 추출** — EXIF/IPTC/XMP·MP4 atom·ID3/Vorbis·PDF/OOXML → 키-값(순수 Rust: kamadak-exif·mp4·lofty·lopdf·zip+quick-xml). **리네임 `{meta.KEY}` + 정보 패널 공용**([25 §5](25-bulk-rename.md)·[35 §4-1](35-preview-system.md)) | P2 | 대 | — | ☐ 설계 |
| REN-3 | **리네임 UDF δ** — **엔진 추상화**(`RenameUdfEngine` 트레이트+피처 플래그) + **초기 구현 Starlark**(파이썬 부분집합·설치0·+1~3MB). 후속 RustPython/CPython 교체. 플러그인 파일·핫리로드. 설계 [41](41-rename-udf-python.md), 착수 시 ADR | P3 | 대 | REN-2 | ☐ 설계 |

### §9-4. 2026-07-10 — 4차 감사(refactor/004-audit, PR#13) 후속

> 적용분(캐시 이빅션·Count 캐시·DATAS·무할당화·FFI 가드 등)은 §1 A-1/A-4/A-6·E-9에 반영. 아래는 **미적용 후속**(방안 보고서=journal/2026-07-10).

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| A-8 | **터미널 렌더 요소 풀링** — 매 프레임 시각 트리 전면 재구축(최대 400라인)→라인 슬롯 재사용/부분 갱신. 대량 출력 최대 병목 | P2 | 대 | — | ☐ |
| A-9 | 터미널 스크롤백 압축 — 행을 마지막 유효 셀까지만 저장(+`TermCell` 16→12B 패킹). NoWrap 1000컬럼 시 ~12.8MB→수 MB | P3 | 중 | — | ☐ |
| E-12 | MainWindow **장대 메서드 분해** — `OnGridKeyDown` 236줄·`TransferPathsInto` 132줄·생성자 131줄 등 5곳 기계적 추출(E-4 명령 레지스트리의 선행 정리) | P2 | 중 | — | ☐ |
| E-13 | **ABI v8: `NexaRow`에 경로 동봉** — 행 실체화당 P/Invoke 3회(GetRow/RowPath/IsSelected)→1회 | P2 | 중 | — | ☐ |
| E-14 | 하단 도크/미리보기 갱신 **디바운스**(60~100ms) — 화살표 연타 시 행마다 정보·미리보기 재계산 제거 | P2 | 소 | — | ☐ |
| D-5 | Release `PublishReadyToRun` + 콜드 스타트 계측(Stopwatch 5지점·PerfView JIT/XAML 분해) — 측정 먼저 | P2 | 소 | — | ☐ |

### §9-5. 2026-07-10 세션 요청

| ID | 항목 | 우선 | 규모 | 의존 | 상태 |
|---|---|---|---|---|---|
| PREF-9 | **재시작 필요 설정 변경 시 확인창 + 자동 재시작** — 언어 변경 등 재시작이 필요한 설정을 바꾸면 확인 다이얼로그(지금 재시작/나중에)를 띄우고, 승인 시 앱이 스스로 재시작(`AppInstance.Restart()` — 미패키지 실행이면 프로세스 재기동 폴백). 재시작 필요 여부는 설정 항목별 판정 위임으로 선언. 메커니즘=[docs/40 §9](40-preferences-system.md) | P2 | 소~중 | PREF-1 | ✅ (07-10 PR#14 `e87166d` — 실기QA 대기) |
| PKG-1 | **Portable-ready 경로 분기**([12 §3](12-packaging-portable.md)) — `AppPaths` 단일 원천: `portable.ini`/`--portable` 감지 시 설정·세션·언어팩·crash.log = `exe\data\` | P1 | 소~중 | — | ✅ (07-10 PR#15 `f44482e` — 실기QA 대기) |
| PKG-2 | **포터블 zip 산출**(`scripts/make-portable.ps1`) — self-contained 게시(런타임 번들)+검증+마커+zip. 게시 특수 대응 3건(csproj 타겟) = [12 §7](12-packaging-portable.md) | P1 | 중 | PKG-1 | ⏸ **CI 잠정 비활성**(07-11 사용자 — 배포 방향성 재정리 후 재개/제거 판단. 스크립트·AppPaths는 유지) |
| PKG-3 | **CI `package` job** — 태그·수동 실행 시 아티팩트 업로드+릴리스 첨부 | P2 | 소 | PKG-2 | ✅ (07-10 PR#15) — 현재 산출=setup.exe만(포터블 ⏸) |
| PKG-6 | **32비트(x86)/64비트 별도 빌드 필요 여부 검토** — 대상 사용자 OS 분포·WinAppSDK x86 지원·인스톨러 아키 분기(현재 x64 전용, arm64는 스크립트만 지원) 조사 후 결정 | P3 | 소(검토) | — | ☐ 검토 대상(07-11 사용자 등록) |
| PATH-SUG | **경로바 편집 자동완성**(탐색기식) — 구분자/접두사 입력 시 하위 폴더 제안 드롭다운(↑/↓ 미리 채움·Enter/클릭 이동·ESC 닫기). 순수 `PathSuggestions`(xUnit 7)+공급자 주입(컨트롤 IO 비종속) | P1 | 중 | — | ✅ (07-11 — 스크린샷 검증) |
| PKG-4 | **MSIX + 서명**(DR-3 1차) — 패키징 프로젝트/매니페스트 + 인증서 전략([12 §6](12-packaging-portable.md), 비밀=커밋 금지) + winget. 단일 exe(self-extract)도 후속. 서명 조사(07-11): Azure Artifact Signing=한국 개인 불가 · 추천=Store 제출(서명 위임) 또는 OV 클라우드 서명 | P2 | 대 | — | ☐ 인증서/Store 결정 필요 |
| PKG-5 | **클래식 설치기 setup.exe**(Inno, [12 §7](12-packaging-portable.md)) — 사용자 단위 기본·언인스톨러·시작 메뉴. CI `package` job이 zip과 함께 빌드·릴리스 자동 첨부. 서명 전 설치형 채널 | P1 | 중 | PKG-2 | ✅ (07-11 PR#16 `9c242e5` — dispatch 실검증, 릴리스=0.3.2) |

<!-- 예: | X-N | 항목 | 우선 | 규모 | 의존 | 상태 | -->
