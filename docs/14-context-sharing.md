# 14 · 컨텍스트 공유 & 비공개 정보 가이드

> "다른 PC에서 clone 시 정리/메모리된 내용을 바로 공유" + "공개되면 안 되는 내용은 클라우드 등으로 공유".
> 저장소는 **공개 예정**(현재 private, 진행 후 public 전환 · DR-5 소스공개) 이므로 **무엇을 저장소에 두고, 무엇을 저장소 밖에 둘지**를 지금부터 명확히 한다(공개 대비).

---

## 1. 공개 컨텍스트는 저장소로 (clone 시 자동 공유)

다른 PC에서 `git clone` 하면 아래가 그대로 따라와 **즉시 동일 컨텍스트**를 얻는다.

| 무엇 | 위치 | 비고 |
| --- | --- | --- |
| 프로젝트 메모리/컨텍스트 | **`CLAUDE.md`**(루트) | Claude Code가 **자동 로드** → 새 PC/세션 즉시 인계 |
| 현황 요약 | `docs/STATUS.md` | 결정·요구·마일스톤·다음 단계 |
| 결정 기록 | `docs/10`, `docs/06`(ADR) | DR-1~5 |
| 설계 전체 | `docs/00`~`13` | 비전~라이선스 |
| 작업 이력 | `docs/journal/` | 타임스탬프 기록 |

> **온보딩 3단계(다른 PC):** ① clone → ② `CLAUDE.md` + `docs/STATUS.md` 읽기 →
> ③ `scripts/bootstrap.ps1`(Windows) 또는 `cargo test`(맥, 코어).
>
> ※ Claude의 **로컬 메모리**(`~/.claude/.../memory/`)는 clone으로 전달되지 않는다 →
> 그 내용은 이미 `CLAUDE.md`/`docs`에 **반영**되어 공개 공유됨. (로컬 메모리 자체는 동기화 대상 아님)

### 1-1. 자동화/권한 설정도 저장소로 (다른 PC에서 동일 적용)

- **`.claude/settings.json`(커밋됨)** — 개발 작업에 필요한 명령(`git` 일반·`cargo`·`dotnet`·읽기전용)을
  **자동 허용**으로 등록 → 다른 PC에서 clone 해도 **동일하게 불필요한 확인 없이** 진행.
- 파괴적 명령(`rm`·`git reset --hard`·`git clean`·force push·`sudo`)은 `ask`로 **확인 유지**.
- 비밀은 절대 여기에 넣지 않음(아래 §2). 개인 전용 예외는 `.claude/settings.local.json`(gitignore).

## 2. 저장소에 두면 안 되는 것 (비공개) — 커밋 금지 목록

공개 예정 저장소이므로(현재 private라도 공개 대비) 아래는 **절대 커밋 금지**:

| 분류 | 예 |
| --- | --- |
| **서명/키** | 코드 서명 인증서(.pfx)·개인키, GPG/SSH 키, **라이선스 서명 비밀키(Ed25519)** — docs/17 §5-1 (공개키는 커밋 OK) |
| **자격증명** | 클라우드/원격 테스트용 SFTP·S3·OneDrive 토큰, DB 비번 |
| **라이선스 발급 비밀** | 상업 라이선스 키 **서명 시크릿**, 결제/스토어 계정 |
| **사업/법무** | 가격 정책, 매출 계획, 상표/계약 문서 |
| **개인정보/인프라** | 이메일·실명, 내부 호스트/VM 이미지, 사설 IP |

## 3. 비공개 정보 공유 방법 (클라우드/시크릿/암호화)

용도별 권장 채널:

| 용도 | 권장 방식 |
| --- | --- |
| **CI에서 쓰는 비밀**(서명 인증서·API 키) | **GitHub Actions Secrets / Environments**(저장소 설정). 워크플로에서 `${{ secrets.* }}` |
| **개발용 자격증명**(.env) | 로컬 `.env`(gitignore) + 저장소엔 `.env.example`(placeholder)만. 팀 공유는 **비밀번호 관리자**(1Password/Bitwarden 공유 볼트) |
| **코드 옆 보관이 필요한 비밀** | **암호화 커밋**: `git-crypt` 또는 `sops`+`age`로 `secrets/*.enc` 만 커밋, 키는 별도 공유 |
| **사업/법무/가격 문서** | **비공개 채널**: 별도 **private 저장소**(`nexa-dir-private`) 또는 클라우드 문서(Google Drive/Notion) |
| **대용량/바이너리**(VM, 인증서 백업) | 클라우드 드라이브(접근권한 제한) / 회사 스토리지 |

### 권장 구성
- **공개 저장소(`nexa-dir`)**: 코드 + 설계 문서(현재 상태).
- **(필요 시) 비공개 동반 저장소(`nexa-dir-private`)**: 사업·법무·비밀 전략 문서. README에서 "내부 문서는 private"로만 안내.
- **GitHub Secrets**: CI 빌드/서명/배포 비밀.
- **비밀번호 관리자 공유 볼트**: 개인 토큰·테스트 자격증명.

## 4. 사고 예방 장치 (지금 도입)

- **`.gitignore`** 에 비밀 패턴 등록(`.env`, `*.pfx`, `*.pem`, `*.key`, `secrets/`, `appsettings.*.local.json`).
- **`.env.example`** 로 필요한 키 목록만 공유(값은 비움).
- **pre-commit 시크릿 스캐닝**(예: gitleaks) + GitHub **Secret Scanning/Push Protection** 활성화 권장.
- 실수 커밋 시: 키 **즉시 폐기·재발급**, 히스토리 제거(`git filter-repo`)는 보조 수단.

## 5. 체크리스트 (다른 PC에서 시작)

- [ ] `git clone` → `CLAUDE.md` + `docs/STATUS.md` 읽음
- [ ] (Windows) `scripts/bootstrap.ps1` 실행 / (맥) rustup + `cargo test`
- [ ] CI 비밀이 필요하면 GitHub Secrets 확인, 로컬은 `.env`(미커밋) 구성
- [ ] 비공개 문서가 필요하면 private 채널(별도 repo/클라우드) 접근 요청
