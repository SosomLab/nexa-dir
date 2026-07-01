# 작업 기록 — 2026-07-01 11:50:59 (KST)

> 기록 ID: `20260701_115059_nexa-controls-product`
> 이전 기록: `20260701_112409_a1-virtualized-tree-grid`

## 1. 요청 (사용자)
- `VirtualizedTreeGrid` → **`NexaFileGrid`** 개명(완료, 커밋 `4472ff8`).
- `NexaFileGrid`는 차후 **3rd-party 컴포넌트로 판매** 가능하도록 `Nexa.Controls` 제품의 구성원이 됨.
- `Nexa.Controls`는 **Nexa Dir과 동일한 라이선스 방식**으로 설계/개발.

## 2. 반영 — 제품화 + 라이선스
- **라이선스 = Nexa Dir과 동일**: 소스공개 제한형 **PolyForm Noncommercial**(개인·비상업 무료 / 상업 유료, DR-5). 상업 문의 kiros33@sosomlab.com.
- **`Nexa.Controls.csproj` 제품/패키지 메타**:
  - `PackageId=SosomLab.Nexa.Controls`, `Version`, `Product`, `Description`(상업 문의 포함), `PackageProjectUrl`, `RepositoryUrl`, `Copyright`.
  - `PackageLicenseFile=LICENSE.md`(SPDX 비표준이라 파일 참조) + `<None Include="..\..\LICENSE.md" Pack="true">` → NuGet 패키지에 라이선스 포함.
- **ADR-0002 §9-1(제품화)** 추가: 독립 3rd-party 컴포넌트 제품, 동일 라이선스, 퍼미시브+WinUI만 의존(재배포 안전), 코어/도메인 비참조(경계 유지) — 판매 시 별도 저장소 분리도 가능.

## 3. 근거
- `Nexa.Controls`는 이미 **도메인/코어 비의존**(ADR §9) → 그대로 판매 가능한 독립 제품 성립.
- 저장소가 PolyForm Noncommercial이므로 동거해도 라이선스 일관. NuGet 배포 시 `LICENSE.md` 동봉.

## 4. 검증
- `dotnet build app/Nexa.App -c Debug` → **0 Warning / 0 Error**(패키지 메타 정상, pack은 아직 비활성).

## 5. 다음
- (제품화 후속) `README.md`(패키지)·`GeneratePackageOnBuild`/CI pack·버전 정책은 실제 배포 착수 시.
- 기능은 **A2**(NexaFileGrid 컬럼 헤더 + IColumn 모델)로 계속.
