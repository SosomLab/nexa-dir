# 24 · Everything식 인스턴트 파일 검색 (설계)

> 목표: **전 드라이브 파일명을 즉시 검색**(<50ms, NFR-P4) + **실시간 갱신**. Everything(voidtools) 방식 채택.
> 요구: FR-F1/F2/F3([05](05-requirements.md)). 마일스톤 **M3**. 구현 대상 등록.

---

## 1. 핵심 모델 (왜 빠른가)

- **NTFS MFT(Master File Table) 직접 읽기**로 볼륨의 **모든 파일명을 즉시 일괄 열거** → 초기 인덱스를 수 초 내 구축.
  (폴더를 재귀 순회하지 않고 MFT 레코드를 한 번에 읽음 = Everything의 속도 비결)
- **USN Change Journal** 구독으로 생성/삭제/이름변경을 **실시간 증분 반영**(전체 재스캔 없음).
- **인메모리 파일명 인덱스**: 부분문자열·와일드카드·정규식·(선택)퍼지 매칭. 질의는 인메모리라 <50ms.

> 콘텐츠(전문) 검색(FR-F3)은 별개 — **tantivy**로 본문 인덱싱([01](01-architecture.md) §3 `indexer`). 본 문서는 **파일명 인스턴트 검색**.

## 2. 권한 / 폴백

- MFT/USN의 **raw 볼륨 읽기는 관리자 권한** 필요 → 두 가지 방식:
  - (A) 앱 승격 요청, 또는 (B) **소형 권한 헬퍼(서비스)** 가 인덱스를 들고 앱에 IPC 제공(Everything 유사). → 별도 ADR에서 확정.
- **비NTFS(FAT/exFAT)·네트워크·원격(VFS)**: MFT 불가 → **walkdir 스캔 + watcher(notify)** 폴백 인덱싱.
- (옵션) **Everything이 설치된 환경**: Everything IPC/SDK로 그 인덱스를 질의하는 어댑터도 가능(자체 구현과 택일/병행).

## 3. 질의 (Query)

- 매칭: 부분문자열(기본)·`*?` 와일드카드·`/regex/`·(선택)퍼지.
- 필터 문법(FR-F2): `path: ext: size:>10mb dt:thisweek tag:`. 정렬(이름/경로/크기/수정일).
- 결과: **스트림**으로 UI에 점진 표시, 결과를 **가상 폴더처럼** 탐색/작업(FR-F3) — 듀얼 패널·교차 선택과 결합.

## 4. 우리 스택 매핑

| 구성 | 위치 / 기술 |
| --- | --- |
| 인덱스 빌드/갱신 | Rust 코어 `nexa-index` — **MFT/USN**(`#[cfg(windows)]`, windows 크레이트 `DeviceIoControl` FSCTL) + **walkdir/notify 폴백** |
| 질의 엔진 | Rust 코어 `nexa-search` — 인메모리 파일명 매처(부분/와일드/정규식) |
| 콘텐츠 검색 | `nexa-index`(tantivy) — 본문(FR-F3) |
| UI | 검색 바/결과 뷰(가상화), 인터롭으로 결과 스트림 |
| 상주 규율 | 인메모리 인덱스 **메모리 상한·증분**, 유휴 트림(NFR-M2~M4) |

> **맥 개발**: MFT/USN은 Windows 전용 → 맥에선 **폴백 경로(walkdir+notify)** 로 질의 엔진·인덱스 로직을 **빌드/단위테스트** 가능. Windows 전용부는 `#[cfg(windows)]` 격리.

## 5. 단계적 구현 (M3)

- **M3-α**: 폴백(walkdir) 인덱스 + 인메모리 파일명 검색(부분/와일드) + 결과 뷰. (맥 테스트 가능)
- **M3-β**: Windows **MFT 일괄 열거** + **USN 실시간 갱신**(권한 헬퍼/승격 ADR).
- **M3-γ**: 필터 문법·정규식·정렬, 결과를 가상 폴더로 작업, (선택) Everything IPC 어댑터.

## 6. 로드맵/백로그
- [02](02-roadmap.md) M3에 본 설계 반영. 백로그 [04](04-trends-todo.md) "인스턴트 검색" → roadmap 승격.
- 미해결: 권한 모델(승격 vs 헬퍼 서비스)·인덱스 영속화(디스크 캐시) → M3 착수 전 ADR.
