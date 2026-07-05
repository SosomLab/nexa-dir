# 작업 기록 — 2026-06-30 11:47:43 (KST)

> 기록 ID: `20260630_114743_git-identity-and-visibility`
> 이전 기록: `20260630_113021_build-test-doc`

## 1. 요구
- 이 저장소를 **kiros33 GitHub 계정**으로 개발(프로필 분리).
- 저장소가 실제 **private**임을 확인 → 문서(public 전제)와의 불일치 정정.
- 지금까지 진행사항 정리 + push.

## 2. Git 계정 분리 (이 레포 한정)
- 진단: push 인증은 이미 kiros33(SSH alias `kiros33.github.com` → `~/.ssh/kiros33_ed25519`). 그러나 **커밋 author는 글로벌 sybae76**(`sybae76@outlook.com`)로 기록되던 상태.
- 적용(레포 로컬 `.git/config`, 글로벌 미변경):
  - `git config user.name  "Sangyong Bae"`
  - `git config user.email "kiros33@gmail.com"`
- 검증: `git config user.email` → kiros33@gmail.com / `ssh -T git@kiros33.github.com` → "Hi kiros33!".
- 결정: 기존 커밋(과거 sybae76)은 **유지**, 이후 커밋부터 kiros33 적용(히스토리 재작성 안 함).
- 비고: VSCode GitHub 로그인(sybae76)은 git 동작과 무관(PR 확장/Settings Sync용) → 변경 불필요.

## 3. 저장소 가시성 — 실제 PRIVATE 확인 → 문서 정정
- `gh repo view` → `"visibility":"PRIVATE"`. gh는 kiros33로 로그인됨(scope repo). 최근 CI 전부 success.
- 방침(사용자 확정): **현재 private, 어느 정도 진행된 뒤 public 전환 예정**(DR-5 소스공개 방향 유지).
- 정정 반영(시점 단서 추가, "public"→"공개 예정(현재 private, 진행 후 전환)"):
  CLAUDE.md(DR-5·§6) · STATUS(DR-5·가시성 줄) · docs/10(DR-5·OD3·가시성 note) · README · docs/13 · docs/14.
- 공개 대비 **비밀 커밋 금지 규율은 지금부터 적용**(private라도).

## 4. 다음
- M0 첫 기능 단위: 인터롭 Rust↔C# 왕복 PoC(+테스트). 첫 kiros33 author 커밋으로 검증.
