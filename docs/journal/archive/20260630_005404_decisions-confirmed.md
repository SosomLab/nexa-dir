# 작업 기록 — 2026-06-30 00:54:04 (KST)

> 기록 ID: `20260630_005404_decisions-confirmed`
> 이전 기록: `20260630_004142_pathbar-terminal`

## 1. 확정 결정 (사용자 답변)

전체 요구사항 기준 결정 과정 진행 → 4대 결정 확정:
- **DR-1 스택(OD1)** = **안 A: Rust 코어 + WinUI 3** (ADR-0001 Accepted).
- **DR-2 디자인** = **프로툴 고밀도 (Path Finder풍)**.
- **DR-3 배포(OD4)** = **MSIX + Releases + winget** + 포터블 가능 설계.
- **DR-4 AI(OD2)** = **보류** (M5 전 ADR).
→ 통합 결정 기록 `docs/10` 작성, `06` 상태 Accepted, 메모리 갱신.

## 2. 신규 요구 반영

### R11. 개발 환경 (맥 우선 / 필요 시 Windows 안내, 다른 Windows PC 재현)
- **반영:** `docs/11-dev-environment.md` 신설 — 코어는 맥에서 개발/테스트, WinUI 앱은 Windows 필수.
  bootstrap.ps1(winget), .vscode/CI 재현성, VM/CI 옵션. 원칙: OS 비의존 코어 최대화.

### R12. 포터블 단일 exe 배포 (우선순위 아님, 설계 선반영)
- **반영:** `docs/12-packaging-portable.md` 신설 — MSIX(1차) + 포터블 폴더/단일exe 기술과제·
  Portable-ready 원칙(자기완결·설정 위치 분기·전역의존 회피·우아한 저하). 배포 시점 판단.

### R13. 내장 zip 압축 — 향후 지원
- **반영:** FR-M(`05`, 향후지원 P2, Rust `zip`), `04` 백로그. 교차폴더 선택 압축, 인플레이스 탐색(후속),
  7z/rar는 플러그인.

## 3. 진행 (Done)
- [x] `10` 결정 기록, `11` 개발환경, `12` 포터블 설계 신설.
- [x] `06` Accepted, `05` FR-M, `04`/README/메모리 갱신.

## 4. 다음 단계
- **OD3 라이선스 결정 진행** (`docs/13`) — 차후 유료 판매 가능 방향 검토 + 사용자 확정.
- 이후: 리포 스캐폴딩(core/app/CI/bootstrap) → M0 스파이크 → M1.
