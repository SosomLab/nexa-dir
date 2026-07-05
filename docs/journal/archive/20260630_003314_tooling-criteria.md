# 작업 기록 — 2026-06-30 00:33:14 (KST)

> 기록 ID: `20260630_003314_tooling-criteria`
> 이전 기록: `20260630_002903_resource-discipline`

## 1. 신규 판단 기준 (질의)

### R7. 개발 툴체인/배포 기준을 ADR에 추가
- **요구:** ① 별도 개발 툴 설치 없이 **VSCode에서 개발 가능**한지,
  ② **GitHub Actions로 패키징·배포 가능**한지를 판단 기준에 포함.
- **반영:**
  - `06` §2 압력표에 두 기준 추가, **§6 평가 매트릭스 재구성**(가중치에 VSCode10·CI10 추가, 전체 재채점),
    참고 행으로 Rust+Tauri 추가, **§7-2** 신설(VSCode 단독 개발 / Actions 패키징 상세 분석).
  - `05` 제약 C4(VSCode 개발 가능, 풀 VS 불요)·C5(Actions 패키징/배포) 추가.

## 2. 분석 결론
- **(a) VSCode 단독 개발:** 권장안 A에서 **가능**. Rust 코어는 VSCode(맥 포함) 완전 지원;
  WinUI UI는 VSCode 편집 + Windows에서 `dotnet`/MSBuild CLI 빌드(풀 VS 불필요, **VS Build Tools**로 충분,
  winget 설치). XAML 비주얼 디자이너가 필요할 때만 VS. 최약점은 **C++(안 C)** 의 VSCode 단독 개발.
- **(b) GitHub Actions:** **가능**. `windows-latest`에서 빌드→**MSIX**→`signtool` 서명(Secrets)→
  **GitHub Releases**(+winget/Store) 자동화. 단 WinUI/C++는 **Windows 러너 필수**(크로스빌드 불가),
  Rust 코어만은 어디서나 빌드.

## 3. 매트릭스 변화 (합계)
- A 하이브리드 **86** (1위 유지) · D Qt 79 · C C++ 78 · B 순수C# 77 · (참고)Rust+Tauri 79.
- 신규 기준은 주로 C++의 VSCode 점수를 낮춤. 툴체인 단순성 최우선이면 Tauri 최상이나 성능 감점.

## 4. OD1 트레이드오프 (사용자 확인 포인트)
- **성능 최대(네이티브)** vs **툴체인 단순성(VSCode/CLI/CI)**.
  - 권장 A: 둘을 균형(네이티브 + 풀VS 불요 + Actions 가능).
  - 순수 단순성 우선이면 Tauri 검토 가치(단 네이티브 성능 양보).

## 5. 다음 단계
1. OD1 확정(A 권장) → ADR Accepted → M0 스파이크(인터롭/트리/메모리) + CI 스켈레톤(Actions, MSIX) 설계.
