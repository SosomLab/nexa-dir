# 작업 기록 — 2026-06-30 02:07:31 (KST)

> 기록 ID: `20260630_020731_licensing-activation`
> 이전 기록: `20260630_012000_scaffolding`

## 1. 요구

### R16. 설치 패키지 관리자 정책 (선행 처리)
- macOS=brew, Windows=choco 우선→winget, 미설치 시 수동 다운로드 안내.
- 반영: `scripts/bootstrap.sh`(신규, brew), `scripts/bootstrap.ps1`(개편, choco→winget→수동),
  `docs/11` §3/§4-2/§4-5(도구별 ID·수동 URL 표).

### R17. 라이선스 정품 인증(오프라인 1차 / 온라인 2차) + TODO 등록
- 요구: 키 입력 정품 인증, 1차 오프라인·2차 온라인. 별도 인증(키 생성) 프로그램 필요.
  사용자 정보+키를 1개 해시/토큰으로 전달. 절차: ①정보요청(결제 선행 가능) ②인증값 생성 ③입력→기능/기간 잠금 해제.
- 추천/설계: `docs/17-licensing-activation.md` 신설.

## 2. 추천 기술 모델 (요지)
- **비대칭 서명(공개키) 기반 라이선스 토큰** — 클라이언트엔 **공개키만**, **오프라인 검증·위조 불가**.
- 알고리즘 **Ed25519**, 토큰 **PASETO v4.public**(대안 JWT/서명 CBOR). 머신 지문 node-lock(2-of-3 관용).
- 생성기 = **별도 private 툴**(비밀키 보관, 앱·저장소 미포함). 온라인 2차 = **Cloudflare Workers + D1/KV**.
- 기성 비교: Keygen(오픈소스 셀프호스트) 평가.

## 3. TODO 등록
- 로드맵 **M7 · 라이선스 정품 인증**(`02`) L1~L10.
- 트렌드 백로그 **F. 상용화/라이선스 인증**(`04`).
- `13`(라이선스)에서 17 링크(집행 메커니즘).

## 4. 우리 스택 매핑
- 검증: Rust `nexa-license`(ed25519-dalek), 공개키 내장 → 인터롭으로 UI 게이팅.
- 지문: 코어 `fingerprint`. 생성기: `nexa-license-gen`(별도 private repo).
- 온라인: Cloudflare Workers 라이선스 서버.

## 5. 다음
- M0 기능 단위(인터롭/스트리밍 열거) 계속. M7은 상용화 단계에서 L1(키관리)부터.
