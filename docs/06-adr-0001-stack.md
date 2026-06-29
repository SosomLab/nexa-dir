# 06 · ADR-0001 — 네이티브 언어 / 스택 결정

- **상태:** Proposed (개발 착수 전 사용자 확정 대기)
- **맥락:** "성능 최우선 네이티브" 요구 + 모던 Fluent UI + AI/클라우드 + macOS 개발/Windows 타깃.
- **관련:** [05-requirements.md](05-requirements.md) OD1, [01-architecture.md](01-architecture.md).

> 본 문서는 **개발 전 언어/스택 확정**을 위한 상세 분석이다. 결정 후 상태를 `Accepted`로 바꾸고
> journal에 확정 시각을 남긴다.

---

## 1. 결정해야 할 것

1. **UI 프레임워크** — 무엇으로 네이티브 창/뷰를 그릴 것인가.
2. **코어(엔진) 언어** — 파일 I/O·인덱싱·디코딩·VFS·AI 핫패스를 무엇으로 짤 것인가.
3. **둘의 결합 방식** — 단일 언어 vs 하이브리드 인터롭.

## 2. 핵심 요구가 스택에 주는 압력

| 요구 | 함의 |
| --- | --- |
| NFR-P1~P5 (즉시 반응) | 무 GC 또는 GC 제어 가능한 핫패스, 진정한 가상화 |
| **인라인 트리 확장 + 폴더 교차 다중 선택** (플래그십) | 대규모 **가상화된 트리** + 임의 경로 집합 선택 모델 → UI 프레임워크의 가상화 트리 성능이 중요 |
| 셸 통합(썸네일/컨텍스트메뉴/IFileOperation) | Windows 네이티브 상호운용 1급 |
| AI/임베딩·압축·암호화 라이브러리 | 풍부한 시스템/네이티브 생태계 |
| macOS 개발 | 코어는 OS 비의존 빌드·테스트 가능해야 유리 |
| **VSCode만으로 개발(풀 Visual Studio 불필요)** | CLI 빌드(`cargo`/`dotnet`·MSBuild) 가능성, IDE 종속 최소화 |
| **GitHub Actions로 패키징·배포** | `windows-latest` 러너에서 빌드+MSIX+서명+릴리스 자동화 가능성 |

## 3. UI 프레임워크 후보

| 후보 | 네이티브 | 가상화 트리 | 셸 통합 | 성숙도 | 비고 |
| --- | --- | --- | --- | --- | --- |
| **WinUI 3 (Windows App SDK)** | ✅ | ItemsRepeater/TreeView(주의 필요) | ★★★★★ | ★★★☆ | MS 공식 모던, Fluent/Mica |
| WPF | ✅ | VirtualizingStackPanel 성숙 | ★★★★ | ★★★★★ | 안정적이나 디자인 구식, 고DPI 보완 필요 |
| Win32 + Direct2D 자체 렌더 | ✅✅ | 완전 자체 제어(최고 성능) | ★★★★★ | ★★ | 최고 성능·최고 비용, 위젯 전부 자작 |
| Qt (C++) | ✅ | QAbstractItemModel 가상화 우수 | ★★★ | ★★★★★ | 강력하나 Fluent 룩·라이선스 고려 |
| Electron/Tauri(웹) | ❌(웹뷰) | DOM 가상화 | ★★ | ★★★★★ | 네이티브 요구 미달 → 제외 |

> 플래그십(대규모 가상화 트리 + 교차 선택)은 **모델/렌더를 우리가 통제**할수록 유리.
> WinUI는 `TreeView` 기본 제공이나 초대형 트리 가상화는 **flattened 가상 리스트 자체 구현**으로 보완 권장.

## 4. 코어 언어 후보

| 후보 | 성능 | 메모리 안전 | OS 비의존 빌드 | 생태계(검색/이미지/SSH/S3) | 인터롭 |
| --- | --- | --- | --- | --- | --- |
| **Rust** | ★★★★★ | ★★★★★ | ★★★★★ | tantivy/image/ssh2/aws-sdk 등 풍부 | C ABI 안정 |
| C++ | ★★★★★ | ★★ | ★★★★ | 방대하나 안전성 부담 | 네이티브 |
| C# (.NET) | ★★★★ (AOT시 ★★★★☆) | ★★★★ (GC) | ★★★★ | 풍부 | UI와 동일언어 |

## 5. 종합 후보안 (End-to-end)

### 안 A — 하이브리드: **Rust 코어 + WinUI 3(C#) UI** ★ 권장
- 장점: 핫패스 무 GC 성능 + 모던 네이티브 UI 생산성, 코어 이식성(macOS 테스트), 안전성.
- 단점: 인터롭(경계 설계·마샬링) 비용, 두 언어 빌드 파이프라인.

### 안 B — 단일: **순수 C#/.NET + WinUI 3**
- 장점: 단일 언어·최고 생산성, 셸 통합 쉬움, NativeAOT로 성능 보완.
- 단점: GC 일시정지가 초대형 트리/대량 작업에서 변수, "최대 성능" 요구에 한 끗 부족 가능.

### 안 C — 단일: **순수 C++/WinRT + WinUI 3 (또는 Win32+Direct2D)**
- 장점: 이론상 최고 성능, 렌더/메모리 완전 통제(플래그십 트리에 유리).
- 단점: 개발·유지비 최고, 안전성 부담, 일정 리스크.

### 안 D — **Qt + C++ (코어/UI 단일 C++)**
- 장점: 강력한 모델/뷰 가상화(QAbstractItemModel)로 트리·교차선택 구현이 자연스러움, 크로스플랫폼.
- 단점: Windows Fluent 네이티브 룩·셸 통합이 WinUI보다 약함, 라이선스 고려.

## 6. 평가 매트릭스 (가중치)

가중치(합 100): 성능 25 · 플래그십 트리/선택 20 · 셸·네이티브 통합 12 · 개발 생산성/일정 10 ·
안전성/유지보수 8 · 이식성 5 · **VSCode/CLI 툴체인(풀 IDE 불필요) 10** · **GitHub Actions 패키징/배포 10**

| 안 | 성능25 | 트리20 | 통합12 | 생산성10 | 안전8 | 이식5 | VSCode10 | CI10 | **합** |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **A 하이브리드(Rust+WinUI)** | 23 | 16 | 11 | 8 | 8 | 5 | 6 | 9 | **86** |
| B 순수 C#/WinUI | 18 | 14 | 11 | 10 | 6 | 3 | 6 | 9 | **77** |
| C 순수 C++/WinRT | 25 | 18 | 12 | 5 | 4 | 3 | 3 | 8 | **78** |
| D Qt/C++ | 23 | 19 | 7 | 7 | 5 | 5 | 6 | 7 | **79** |
| (참고) Rust+Tauri(웹뷰) | 17 | 14 | 7 | 9 | 7 | 5 | 10 | 10 | **79** |

> 점수는 의사결정 보조용 상대값. 두 신규 기준은 주로 **C(순수 C++)의 VSCode 점수를 낮춤**.
> 툴체인·CI를 최우선으로 보면 **Rust+Tauri**가 최상이나 "네이티브 최대 성능" 요구에서 감점 → 균형상 **A** 유지.

## 7. 권장 (Recommendation)

> **안 A — Rust 코어 + WinUI 3(C#) UI** 를 권장.
> 단, 플래그십(대규모 가상화 트리 + 폴더 교차 다중 선택)은 **WinUI 기본 TreeView에 의존하지 않고**,
> 코어가 만든 *flattened 가시 노드 스트림*을 `ItemsRepeater` 가상화로 렌더하는 **자체 트리 모델**로 구현한다.

### 리스크 헤지
- M0에서 **두 개의 스파이크**를 먼저 실행해 확정 근거를 만든다:
  1. **인터롭 스파이크**: Rust→C# 경계로 10만 노드 가상 트리 스트리밍 + 스크롤 60fps 측정.
  2. **선택/작업 스파이크**: 서로 다른 폴더의 임의 경로 집합 선택 → 복사/이동 잡 수행.
- 스파이크 결과가 목표(NFR-P1~P5)를 못 맞추면 안 C/D로 후퇴 경로를 둔다.

## 7-1. Rust의 Windows API/시스템 콜 처리 능력 (질의 답변)

> "Rust가 Windows system call 처리에서 C++ 대비 부족하지 않은가?"

- **API 커버리지: 사실상 동등.** Microsoft 공식 [`windows`](https://github.com/microsoft/windows-rs) 크레이트가
  Win32 + COM + WinRT 전체를 **win32metadata에서 자동 생성**하여 제공. SDK 갱신마다 재생성 → 항상 최신.
  Shell(IShellFolder/IContextMenu/IExplorerCommand/IFileOperation), 썸네일(WinRT) 등 우리가 쓸 표면 모두 포함.
- **호출 메커니즘 동일.** Windows엔 Linux식 안정 raw syscall ABI가 없어 C++도 ntdll/kernel32 **DLL**을 거침.
  Rust도 같은 DLL을 **제로코스트 FFI**로 호출 → 성능·접근성에 본질적 차이 없음.
- **C++가 더 편한 지점(에르고노믹스):** COM/WinRT 구현(특히 **in-proc 셸 확장 DLL** 작성), C++/WinRT 언어 프로젝션,
  ATL/MFC 편의, 예제·문서가 C++ 우선. windows-rs의 COM은 `unsafe`가 많고 더 장황.
- **우리 아키텍처가 이 약점을 회피:** 안 A에서 **COM·셸 통합은 C#/WinUI 계층**에 둔다(C#은 COM 상호운용·CsWin32·
  WinRT 프로젝션이 매우 우수). **Rust 코어는 파일 I/O·인덱싱·디코딩·VFS** 등 COM 의존이 적고 Rust가 가장 강한 영역 담당.
  → 역할 분담으로 "C++의 COM 편의" 이점을 상당 부분 무력화.
- **결론:** 커버리지·성능은 부족하지 않음. 차이는 **COM/셸 확장 작성의 편의성**이며, 이를 C# 계층에 배치해 해소.
  만약 코어에서 **무거운 COM/인프로세스 셸 확장**을 직접 많이 짤 계획이면 C++(안 C)가 더 편함 → OD1 판단 요소.

## 7-2. VSCode 단독 개발 & GitHub Actions 패키징/배포 (판단 기준)

> "별도 개발 툴(풀 Visual Studio) 설치 없이 VSCode로 개발 가능한가?" +
> "GitHub Actions로 패키징·배포 가능한가?"

### (a) VSCode만으로 개발
- **공통 사실:** Windows 앱은 macOS에서 **빌드/실행 불가** → 어느 안이든 **Windows 빌드 머신(VM/물리/CI)**
  이 필요. 단, *편집*은 VSCode로, *빌드*는 Windows에서 CLI로 하는 워크플로우는 모든 안에서 가능.
  "별도 툴 없이"는 보통 **"풀 Visual Studio IDE 없이"**를 의미 — SDK(.NET/Windows/Windows App SDK)나
  **VS Build Tools**(IDE 아님, CLI 전용)는 winget으로 설치 가능.

| 안 | VSCode 단독 개발 | 비고 |
| --- | --- | --- |
| Rust 코어 | ★★★★★ | rust-analyzer + `cargo`, **macOS에서도 그대로** 편집·테스트 |
| A/B WinUI 3(C#) | ★★★ | `dotnet`/MSBuild CLI 빌드 가능(+C# Dev Kit). 단 **XAML 디자이너·핫리로드는 VS 강점**, 일부 템플릿이 VS 가정 |
| C C++/WinRT | ★★ | MSVC 빌드툴 필요, C++/WinRT+XAML 컴파일러가 **VS 종속 강함** → VSCode 단독은 거침 |
| D Qt/C++ | ★★★ | CMake + CMake Tools로 VSCode 가능, 단 Qt SDK·컴파일러 설치 필요 |
| (참고)Tauri | ★★★★★ | 순수 CLI(`cargo`/npm), VSCode 친화 최상, macOS에서 크로스 개발 용이 |

- **권장안 A 결론:** **가능**. Rust 코어는 VSCode(맥 포함) 완전 지원. WinUI UI는 VSCode에서 편집 +
  Windows에서 `dotnet build`/MSBuild로 빌드(풀 VS 불필요, VS **Build Tools**로 충분). XAML 비주얼
  디자이너가 필요하면 그때만 VS 사용. → "풀 VS 강제"는 아니다.

### (b) GitHub Actions 패키징·배포
- **공통 사실:** `windows-latest` 러너에 VS Build Tools·Windows SDK·.NET이 사전 설치 → 모든 네이티브 안 빌드 가능.

| 단계 | 방법 |
| --- | --- |
| 빌드 | Rust: `cargo build --release`; WinUI: `dotnet publish`/`msbuild` (windows-latest) |
| 패키징 | **MSIX**(`makeappx`/Windows App SDK) 또는 비패키지 EXE, 자기완결(self-contained) 배포 |
| 서명 | `signtool` + 인증서(Actions **Secrets**), (옵션) 신뢰 서명/Azure Trusted Signing |
| 배포 | **GitHub Releases** 자동 업로드, (옵션) **winget** 매니페스트, MS Store 제출 |
| 업데이트 | MSIX App Installer 또는 자체 업데이터 채널 |

- **결론:** **가능**. 빌드→MSIX→서명→Releases 전 과정을 Actions로 자동화. 단 **Windows 러너 필수**
  (WinUI/C++는 크로스빌드 불가), 코어(Rust)만이라면 어디서나 빌드 가능.

### 종합
- 두 기준 모두 **권장안 A에서 충족 가능**. 가장 큰 마찰은 **C++(안 C)의 VSCode 단독 개발**이며,
  이는 매트릭스에서 C의 약점으로 반영됨.
- 만약 **"VSCode 단독 + 최소 툴 + CI 단순함"을 P0 제약**으로 둔다면 Rust+Tauri가 최적이나
  네이티브 성능에서 감점 → 사용자 우선순위(성능 vs 툴체인 단순성) 확인이 OD1의 핵심 트레이드오프.

## 8. 결정 절차

1. 본 ADR 검토 → 사용자 확정(안 A/B/C/D 중).
2. 확정 시 상태 `Accepted`, journal에 시각 기록, `01-architecture.md` 동기화.
3. M0 스파이크로 가정 검증 → 실패 시 ADR-0002로 재결정.

## 9. 미해결

- AI 제공자/프라이버시(OD2), 라이선스(OD3), 패키징(OD4)은 별도 ADR.
