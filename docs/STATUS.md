# STATUS — Nexa Dir 진행 현황

> 갱신: 2026-06-30 (KST) · 단계: **스캐폴딩 완료, M0 진행 중** · 코어 cargo test green

## 1. 확정된 결정 (Decision Record [10](10-decision-record.md))

| # | 영역 | 결정 |
| --- | --- | --- |
| DR-1 | 기술 스택 | **Rust 코어(cdylib) + WinUI 3(C#/.NET8)** · ADR-0001 Accepted |
| DR-2 | 디자인 | **프로툴 고밀도(Path Finder풍)**, 다크 기본, 키보드 우선 |
| DR-3 | 배포 | **MSIX + Releases + winget** + 포터블(폴더/단일exe) 가능 설계 |
| DR-4 | AI | 보류 (M5 전 별도 ADR) |
| DR-5 | 라이선스 | **소스공개 제한형 PolyForm Noncommercial**(대안 BSL), **공개 예정(현재 private, 진행 후 public 전환)**, 개인무료/상업유료 |

## 2. 핵심 요구 (확정)

- ★ **플래그십**: macOS식 인라인 폴더 확장 + **폴더 교차 다중 선택** + 혼합 일괄 작업 (M1)
- **좌/우 듀얼 패널 + 패널별 싱글/멀티라인 탭** (M1)
- **상단 계층 경로 바(브레드크럼)** — 우선 구현 (M1)
- **컨텍스트 메뉴**: Windows 셸 메뉴 호스팅 + 고유 항목 병합 (M1)
- **퀵 런처 바**(외부 터미널/에디터, M1) · **하단 임베디드 터미널 패널**(ConPTY, 설계동결/구현후속)
- **상주 규율**: 저메모리·주기 정리·무간섭·오류 격리 (전 단계 NFR)
- **클라우드/네트워크**(SMB→SFTP→S3→SaaS, M4, 단계적)
- **플러그인**(WASM+Python/Node RPC, M6) · **내장 zip**(향후 지원)
- **라이선스 정품 인증**(오프라인 1차/온라인 2차, Ed25519, M7) → [17](17-licensing-activation.md)

## 3. 마일스톤

- **M0** 기반: 인터롭 PoC, 스트리밍 열거, 가상화 렌더, CI/부트스트랩
- **M1** ★ 1순위 묶음: 계층 경로 바 · 탭/듀얼 · **플래그십(인라인 트리+교차선택)** · 컨텍스트 메뉴 · 퀵 런처
- **M2** 미리보기 · **M3** 검색 · **M4** 클라우드/원격 · **M5** AI · **M6** 플러그인 · **M7** 라이선스 인증

## 4. 개발 모델 ([11](11-dev-environment.md))

- 코어(Rust): **맥에서 개발/테스트 가능** · 앱(WinUI): **Windows 필수**(PC/VM/CI).
  실측: **dotnet은 맥 가능, WinUI 3는 맥 불가**(XAML 컴파일러 Windows 전용) → docs/11 §6-1.
- 환경 설치: **macOS=`bootstrap.sh`(brew)** · **Windows=`bootstrap.ps1`(choco→winget→수동)**, 도구 표 docs/11 §4-5.
- 다른 PC: clone → bootstrap → 빌드 · CI(windows-latest)가 신뢰 원천.

## 5. 개발 방식 ([15](15-dev-methodology.md))

- **작은 기능 단위(수직 슬라이스) 순차** · **단위=커밋 1개** · **초안→확장 프로토타이핑** · main 항상 green.
- 단위 백로그(M0→M1)는 docs/15 §7. 커밋은 Conventional Commits.

## 6. 남은 Open / 다음 단계

- OD2 AI(보류), 상표 출원 검토. LICENSE(영문/한글)·THIRD-PARTY-NOTICES ✅ 추가됨.
- **스캐폴딩 완료** ✅:
  - `core/`(Rust 워크스페이스 nexa-core/vfs/interop, **cargo test green**) · `app/Nexa.App`(WinUI 스켈레톤, Windows 빌드)
  - CI(.github/workflows) · LICENSE.md/LICENSE.ko.md/THIRD-PARTY-NOTICES · `.gitignore`/`.claude/settings.json`
  - 환경: `scripts/bootstrap.sh`(brew)·`scripts/bootstrap.ps1`(choco→winget→수동)·global.json·.vscode
- **Windows 풀빌드 실측 검증** ✅ (2026-06-30): bootstrap.ps1로 Rust 1.96 + VS Build Tools 2022 + .NET SDK 9 설치 →
  `cargo test`(코어 green) + `dotnet build app/Nexa.App`(경고0/오류0). bootstrap의 .NET SDK 감지는 `dotnet --list-sdks` 기준으로 개선.
  - 메타: 조직/연락처(SosomLab) 반영(README/LICENSE/Cargo.toml/csproj)
- **M0 인터롭 왕복 PoC 완료** ✅ (2026-06-30): Rust `nexa-interop`의 C ABI `nexa_poc_add` ↔ C# P/Invoke(`NativeInterop.cs`).
  csproj가 cargo로 cdylib 빌드 → `nexa_interop.dll`을 앱 출력에 복사, CI app job에 Rust 툴체인 추가. **CI success**.
  검증: dll 왕복 `abi=1 / poc_add(2,3)=5 / (40,2)=42`. (커밋 `c41dc41` 초안 + `af07fee` 확장) · 절차 [18](18-build-and-test.md) §2-1.
- **M0 로컬 스트리밍 열거 완료** ✅ (2026-06-30): `nexa-vfs::read_dir_entries` — 점진 산출 Iterator(엔트리별 Result, 메타 실패 격리).
  `Entry`에 size/modified 추가. 맥/Win 빌드·테스트(7 tests green). 플래그십 인라인 트리의 기반(FR-A1).
- **M0 인터롭 디렉터리 열거 API 완료** ✅ (2026-06-30): `nexa_dir_open`/`nexa_dir_next`/`nexa_dir_close` + `NexaEntry`(name/kind/size/modified_unix_ms).
  핸들 기반 스트리밍, 엔트리 오류 격리, name 수명은 핸들 보관. 9 tests green.
- **M0 디렉터리 열거 C# 바인딩+UI 완료** ✅ (2026-06-30): `NativeInterop.ReadDir`(open/next/close 래핑) → MainWindow ListView에
  홈 폴더 목록 표시(종류/이름/크기). 코어 스트리밍 열거가 화면까지 도달. 빌드 0/0, 앱 기동 검증.
- **다음 단위(M0)**: WinUI **ItemsRepeater 가상화 렌더**(대량 항목) → 경로 입력/네비게이션.
  (권장: 맥 빌드 가능한 `Nexa.ViewModels`(net8.0) 분리 — docs/11 §6-1)

## 7. 다른 PC에서 시작 / 컨텍스트 공유

- clone 후 **`CLAUDE.md`(자동 로드) + 이 STATUS** 로 즉시 인계 → [14](14-context-sharing.md).
- **저장소 가시성**: 현재 **private**, 어느 정도 진행 후 **public 전환 예정**(DR-5 소스공개 방향). 공개 대비 지금부터 비밀 커밋 금지 규율 적용.
- 비밀(서명 인증서·자격증명·라이선스 키·사업/법무)은 **저장소 밖**(GitHub Secrets·비밀번호 관리자·
  private repo·암호화 커밋). `.gitignore`로 사고 예방.

## 8. 문서 인덱스

CLAUDE.md(이식 메모리) · 00 비전 · 01 아키텍처 · 02 로드맵 · 03 기능 · 04 트렌드백로그 ·
05 요구사항 · 06 ADR-0001(스택) · 07 플래그십 · 08 경쟁조사 · 09 플러그인 · 10 결정기록 ·
11 개발환경 · 12 포터블 · 13 라이선스 · 14 컨텍스트공유 · 15 개발방법론 · 16 프로젝트구조 ·
17 라이선스인증 · 18 빌드&테스트 · 19 구현기능현황 · journal/ (작업기록)
