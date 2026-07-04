# 33 · 파일 조작 심화 설계 — DnD 정책 · 폴더 hover · 자동 갱신 · Undo/Redo

> 상태: **설계(구현 대기)**. 2026-07-04 세션 요청 정리. 원장·상태 = [TASKS.md](TASKS.md), 버그 = [BUGS.md](BUGS.md).
> 관련: [21 ADR-0002 파일뷰](21-adr-0002-fileview-control.md) · [TODO B-1/B-2/C-3](TODO.md) · 컨텍스트/클립보드 기구현(트랙 B-8~B-14).

---

## B-14dnd · DnD 디스크별 기본 동작 + 표준 수정키(Ctrl/Shift)

### 요구
- **같은 디스크(볼륨)**: 이동(Move)을 기본.
- **다른 디스크**: 복사(Copy)를 기본.
- **수정키(Windows 표준)**: **Ctrl=복사 강제 · Shift=이동 강제**(기본 동작 override). ~~Alt 반전~~은 Alt가 OS 메뉴 활성화에 가로채여 신뢰 불가 → 표준 채택(사용자 결정 2026-07-04).

### 설계
- **볼륨 판정**: `Path.GetPathRoot(GetFullPath(src))` vs `dest` 루트 비교(대소문자 무시). 같으면 동일 볼륨. UNC/네트워크는 루트(`\\server\share`) 기준. 판정 실패 시 **이동**(같은 디스크 취급, 보수적).
- **수정키**: `DragEventArgs.Modifiers`의 `Control`/`Shift` 사용(OS 드래그 루프에서 신뢰 가능·가로채임 없음).
- **연산 결정**: `if (Ctrl) Copy; else if (Shift) Move; else (sameVolume ? Move : Copy)`.
- **반영 지점**(전 드롭 경로 일관):
  - `OnRowDragOver`(폴더): `e.AcceptedOperation = op`, 캡션 "…(으)로 이동/복사".
  - `OnTabDragOver`(탭): dest=`tab.Current`로 op 계산.
  - `OnBodyDragOver`(빈영역, 컨트롤): 볼륨 미인지 → **Alt만 반영**(Alt=Copy/없음=Move)한 근사 커서, 실제 연산은 드롭 시 호스트가 확정.
  - **드롭**: 각 핸들러가 op 재계산 → `TransferPathsInto(paths, destDir, op)`(copy/move 분기, 제자리 이동 제외, 양쪽 재로드).
- **구조**: 기존 `MovePathsInto` → **`TransferPathsInto(sourceLeft, paths, destDir, op)`**(FileOps.CopyInto/MoveInto 위임). `BodyDropped` 이벤트에 `DragDropModifiers` 전달(빈영역 드롭의 op 계산용).
- **캡션**: `DragUIOverride.Caption`에 "복사"/"이동" 명시(사용자 예측성).

### 범위/주의
- 휴지통·심링크 대상 볼륨 예외는 후속. 대용량 복사 진행률/취소는 nexa-ops(B-1) 전까지 동기(주의).

---

## B-15h · 드래그 중 폴더 hover 진입(spring-loaded) + 전환시간 설정

### 요구
- 드래그 중 **폴더 위 3초 hover** → 그 폴더로 **전환(진입)** 되어 더 깊이 드롭 가능.
- 탭 hover(구현됨, 2초)와 함께 **전환시간을 설정에서 지정**: 탭 2초·폴더 3초 기본.

### 설계
- **폴더 dwell 타이머**: `OnRowDragOver`에서 대상이 폴더고 이전 dwell 대상과 다르면 타이머 재시작(`FolderDwellMs`, 기본 3000). Tick → 그 폴더로 **Navigate 진입**(현 패널; 진입 후 계속 드래그해 하위에 드롭). `DragLeave`/`Drop`에서 취소.
  - 진입 후 원 선택/드래그 상태(`_dragPaths`)는 유지. 진입은 spring-load라 **네비 기록에 남기지 않거나(record:false)** 별도 처리 고려.
- **탭 dwell**(기존 B-13): 상수 2초 → `TabDwellMs`(기본 2000) 설정값으로 교체.
- **설정**: [ViewOptions](../app/Nexa.App/Settings.cs) `TabDwellMs`(2000)·`FolderDwellMs`(3000). 설정 화면(후속)에서 편집. 0이면 비활성 옵션도 고려.
- **주의**: 폴더 진입이 잦으면 방향 상실 가능 → 진입 시 은은한 시각 피드백(경로바 강조) 권장. spring-load 진입을 "임시"로 표시하고 드롭 취소 시 원위치 복귀는 후속.

---

## B-12w · 자동 갱신(watcher) — Pub/Sub + 수동 새로고침 + 성능

### 요구
- 폴더/파일 변경 시 자동 갱신. 자기 패널 붙여넣기는 갱신되나 **다른 패널이 같은 폴더를 보면 미갱신**([BUG-006](BUGS.md)).
- 특정 폴더/파일 변경을 모니터링·통지하는 **Pub/Sub** 모델 — **성능 이슈 검토** 필요. **수동 갱신**(F5, 구현됨)도 유지.

### 설계 — 계층
1. **감시원(코어 또는 앱)**: OS 변경 통지. 선택지:
   - **앱 계층 `FileSystemWatcher`**(간단·즉시): 각 **활성 탭의 현재 폴더**만 구독(비재귀). 트리 펼침 하위는 옵션(재귀는 비쌈 → 기본 비재귀, 펼친 폴더만 개별 구독).
   - **코어 `notify` 크레이트**(장기·이식): 코어 VFS Provider 계약(C-2)과 함께. M4 원격 Provider까지 일관. → 정석이나 규모 큼.
   - **권장 단계**: 1차 앱 `FileSystemWatcher`(현재 폴더 구독)로 BUG-006 근본 해결 → 이후 코어 이관(C-2/C-3).
2. **Pub/Sub 브로커**: `IFolderChangePublisher` — 경로별 구독자(패널/탭) 관리. 변경 이벤트(생성/삭제/이름변경/수정) 발행 → **그 경로를 보는 모든 패널/탭**이 갱신 구독.
   - 구독 키 = **정규화 폴더 경로**. 여러 패널이 같은 폴더면 1개 watcher 공유(참조 카운트).
3. **성능(핵심 검토)**:
   - **구독 범위 최소화**: 재귀 감시 금지(기본). **보이는 폴더(활성 탭 현재 경로 + 펼친 폴더)만** 구독. 탭 전환/접힘 시 구독 해제.
   - **디바운스/코얼레싱**: 짧은 시간 다발 이벤트(대량 복사)를 **묶어 1회 갱신**(예: 200~500ms 디바운스). 이벤트 폭주 시 "전체 재열거 1회"로 폴백.
   - **UI 스레드 보호**: watcher 콜백은 백그라운드 → 디바운스 후 `DispatcherQueue`로 1회 재로드(P1 백그라운드 열거 재사용).
   - **상한**: 동시 watcher 수 상한(다중 탭). 초과 시 활성 탭 우선.
   - **결론**: 경로 구독 범위 제한 + 디바운스 + 비재귀면 **성능 이슈 관리 가능**. 재귀·전역 감시는 금지.
4. **수동 갱신**: F5(구현됨) + 빈영역 메뉴 "새로고침"(구현됨) 유지 — watcher 실패/원격 대비 항상 제공.
5. **선택 정리**: 갱신 시 삭제된 항목이 선택/캐럿이면 제거(코어 트리 재열거로 자연 정리, ADR-0004 후속 훅과 연계).

### 임시(현재 적용): 파일 작업 후 **양쪽 패널 재로드**([7b9e959]) — watcher 도입 시 정밀 갱신으로 대체.

---

## B-13u · Undo/Redo — 동작 히스토리 클래스

### 요구
- 복사·이동·**일반삭제(휴지통)** 를 **Ctrl+Z(undo)/Ctrl+Y(redo)**. 별도 히스토리 관리 클래스 필요 여부 검토 → **필요**.

### 설계
- **`OperationHistory`**(앱 또는 nexa-ops): `IReversibleOp` 스택 2개(undo/redo). 각 작업 커밋 시 **역연산 정보**와 함께 push, redo 스택 clear.
- **`IReversibleOp`**: `void Undo()` / `void Redo()` + 설명. 구현:
  - **이동**: undo=역방향 이동(dest→src). src 경로·dest 실제경로(충돌 시 부여된 이름) 기록.
  - **복사**: undo=복사본 삭제(휴지통). redo=재복사.
  - **일반삭제(휴지통)**: undo=**휴지통에서 복원**. 휴지통 복원 API 필요(`SHFileOperation`/`IFileOperation` 또는 원경로 기록). ★ 복원은 휴지통 항목 식별이 관건 → nexa-ops의 IFileOperation 경로에서 원자적으로.
  - **완전삭제**: **undo 불가**(설계상 제외, 확인창으로 방어).
  - **이름변경**(B-6): undo=역이름. (선택 포함 가능.)
- **정합성**: undo/redo도 파일시스템을 바꾸므로 watcher/양쪽 갱신과 연동. 외부에서 파일이 이미 바뀌면 undo 실패 → 상태바 알림(무결성 우선, 강제 안 함).
- **범위**: 세션 한정(영속 X, 초기). 다중 항목 배치 작업은 1개 트랜잭션으로 묶어 1회 undo.
- **의존**: 정석은 **nexa-ops(B-1)** 의 트랜잭션·IFileOperation 위에. 임시 C# 구현도 가능하나 휴지통 복원 때문에 IFileOperation 권장 → **B-1과 함께 착수 권장**.

---

## CLIP · 클립보드 개념 & OS 클립보드 연동 옵션

### 현재(앱 내부 클립보드)
- 복사/잘라내기는 **앱 내부 정적 클립보드**(`FileClipboard`)에 경로 목록 + 모드(cut/copy)만 저장. OS 클립보드와 **무관**.
- **문제(사용자 리포트)**: 파일을 복사한 뒤 다른 앱에서 **텍스트를 복사**해도 우리 앱은 이를 모르고, **이전에 복사된 파일이 그대로 붙여넣기**됨. 탐색기는 이 경우 파일 붙여넣기가 무효화된다(OS 클립보드가 텍스트로 덮여서).
- 이유: 우리 클립보드는 OS 클립보드를 **읽지/쓰지 않음** → 외부 변화(텍스트 복사)를 감지 못함.

### 옵션: OS 클립보드 연동(`ViewOptions.UseSystemClipboard`, 기본 false)
- **켜면**(true): 
  - **복사/잘라내기** → OS 클립보드에 `DataPackage`(StorageItems + `Windows.ApplicationModel.DataTransfer`의 **Preferred DropEffect**로 cut/copy 구분)를 기록.
  - **붙여넣기** → OS 클립보드에서 **StorageItems를 읽음**. 파일이 없으면(텍스트 등으로 덮인 경우) **붙여넣기 비활성**(탐색기와 동일).
  - **셸 상호운용**: 탐색기에서 복사한 파일을 우리 앱에 붙여넣기, 그 반대도 됨.
- **끄면**(false, 현재): 앱 내부 클립보드(빠르고 격리, 외부와 무관).
- **구현 주의**: StorageItems는 **비동기**(`StorageFile.GetFileFromPathAsync`), 대량 시 성능. 잘라내기는 붙여넣기 성공 후 원본 제거(이동)를 우리가 수행(OS는 "이동 표시"만). 클립보드 소유권/무효화 이벤트(`Clipboard.ContentChanged`) 구독으로 붙여넣기 가능 여부 갱신.
- **권장**: 옵션으로 제공(기본 내부). 셸 통합(SHELL) 착수 시 함께 구현. i18n·컨텍스트 메뉴 "붙여넣기" 활성/비활성이 OS 클립보드 상태를 반영하도록.

## 연관(별도 문서)

- **COL-2/COL-3 정렬**(3상태·Alt 다중키): [docs/23 §4](23-column-system.md) 갱신됨.
- **COL-4 컬럼 조정 모달**: [docs/23 §6-1](23-column-system.md).
- **SHELL 셸 컨텍스트 메뉴 통합**(`IContextMenu`/`IExplorerCommand`): [TODO §2 B-2](TODO.md) · COM 인터롭이라 **별도 ADR** 후보(호스팅 프로세스·보안·성능).
- **타입어헤드**: [docs/32](32-typeahead-find.md).

## 구현 우선순위(이 문서 항목)

B-14dnd(중, 즉시 가능) → B-15h(중, 설정 동반) → **B-12w 1차**(앱 FileSystemWatcher, BUG-006 근본) → B-13u(대, nexa-ops와) → SHELL(대, ADR).
