# 16 · 프로젝트 구조 (스캐폴딩 결과)

> 스캐폴딩 단계에서 추가된 **폴더/파일의 목적**과 **담긴 정보**를 정리한다.
> 변경 시 본 문서를 갱신한다. (현황: [STATUS.md](STATUS.md))

## 1. 전체 트리

```
nexa-dir/
├─ CLAUDE.md                # 이식용 프로젝트 메모리(Claude 자동 로드) — 다른 PC 즉시 인계
├─ README.md               # 프로젝트 개요·문서 인덱스·라이선스·연락처
├─ STATUS.md → docs/STATUS.md
├─ LICENSE.md              # PolyForm Noncommercial 1.0.0 (영문, 정본)
├─ LICENSE.ko.md           # 라이선스 한글본(참고용, 정본은 영문)
├─ THIRD-PARTY-NOTICES.md  # 의존성 라이선스 고지(퍼미시브)
├─ global.json             # .NET SDK 버전 핀(rollForward latestMajor)
├─ .gitignore              # 비밀/빌드산출물 제외 패턴
├─ .claude/
│  └─ settings.json        # 권한 자동 허용(dev 명령) — 다른 PC 동일 적용
├─ .github/workflows/
│  └─ ci.yml               # CI: 코어(mac/win) test·라이선스 게이트·WinUI 빌드
├─ .vscode/
│  ├─ extensions.json      # 권장 확장(rust-analyzer, CodeLLDB/cpptools 등)
│  ├─ settings.json        # rust-analyzer linkedProjects=core 등
│  └─ launch.json          # Rust 디버그(F5) — macOS/Linux CodeLLDB cargo 통합
├─ scripts/
│  ├─ bootstrap.sh         # macOS 개발환경 설치(brew) + 수동 폴백
│  └─ bootstrap.ps1        # Windows 설치(choco→winget→수동 폴백)
├─ core/                   # ── Rust 코어 워크스페이스(핫패스) ──
│  ├─ Cargo.toml           # 워크스페이스 정의(resolver 2, release LTO)
│  ├─ Cargo.lock           # 의존성 잠금(재현 빌드 — 커밋)
│  ├─ rust-toolchain.toml  # Rust stable + rustfmt/clippy 고정
│  ├─ deny.toml            # cargo-deny 라이선스 게이트(퍼미시브 온리)
│  └─ crates/
│     ├─ nexa-core/        # 공용 타입(FileKind, CORE_VERSION)
│     ├─ nexa-vfs/         # 가상 파일시스템 추상화(Provider trait, Entry) + 로컬 스트리밍 열거 read_dir_entries·가시성 필터
│     ├─ nexa-tree/        # 인라인 트리 + 교차선택(가시행 평면 스트림 VisibleRow + OrderedSet 선택, C1/ADR-0004)
│     └─ nexa-interop/     # C ABI cdylib(nexa_abi_version + nexa_tree_*, ABI v7) — C# 인터롭 표면
├─ app/                    # ── WinUI 3 UI 셸(Windows 전용) ──
│  ├─ README.md            # 앱 빌드 안내(Windows)
│  ├─ Nexa.Controls/       # 재사용 WinUI 컨트롤 라이브러리 — NexaFileGrid(ItemsRepeater 래핑·도메인 비종속) → ADR-0002 §9
│  ├─ Nexa.Plugins/        # 플러그인 SDK/계약(IPreviewProvider·PreviewRequest 등) — 퍼미시브 MIT(DR-6), 누구나 플러그인 개발 → docs/36
│  ├─ Nexa.Plugins.Samples/ # 플러그인 샘플(텍스트/이미지 미리보기 공급자, MIT) — 앱 내장으로도 등록(dogfooding)
│  ├─ Nexa.ViewModels/     # UI 비종속 순수 로직(net8.0) — PathDisplay·NavigationHistory. 맥/Win 공통 테스트(B-1)
│  ├─ Nexa.ViewModels.Tests/ # 위 순수 로직 xUnit 테스트(net8.0, 크로스플랫폼 CI)
│  └─ Nexa.App/
│     ├─ Nexa.App.csproj   # WinUI3, net8.0-windows, 비패키지 + cargo(nexa-interop)→dll 복사 타겟
│     ├─ App.xaml(.cs)     # 앱 진입점·리소스
│     ├─ MainWindow.xaml(.cs) # 메인 윈도우(듀얼 패널·탭·경로바·인라인 트리 소비·비동기 로드; 955줄, 트랙 B로 계속 축소)
│     ├─ VirtualTreeCollection.cs # 코어 트리 가시행 스트림을 가상화로 소비(백그라운드 OpenAndExpand + AdoptHandle)
│     ├─ NativeInterop.cs  # 코어 cdylib P/Invoke 바인딩(nexa_abi_version/nexa_poc_add + nexa_tree_* 마샬 은닉)
│     └─ app.manifest      # 고DPI(PerMonitorV2)·지원 OS
├─ tools/                  # (예정) nexa-license-gen — 라이선스 키 생성 CLI(비밀키는 외부)
└─ docs/                   # 설계·결정·작업기록 (00~23 + STATUS + journal)
```

> `tools/`는 M7(라이선스 인증) 착수 시 추가. 생성기 코드는 public 가능, **비밀키 파일만 외부 격리**([17](17-licensing-activation.md) §5-1).

## 2. 디렉터리/파일별 목적

### 2-1. 루트 메타·라이선스
| 파일 | 목적 / 담긴 정보 |
| --- | --- |
| `CLAUDE.md` | 다른 PC/새 세션이 즉시 컨텍스트를 잇도록 하는 휴대용 메모리(결정 DR-1~5, 요구, 규약) |
| `README.md` | 개요·설계 원칙·문서 인덱스·라이선스·프로젝트 정보(연락처) |
| `LICENSE.md` / `LICENSE.ko.md` | PolyForm Noncommercial(영문 정본 / 한글 참고). 상업 사용 유료 안내 |
| `THIRD-PARTY-NOTICES.md` | 의존성 라이선스 고지. CI에서 `cargo deny`로 퍼미시브 강제 |
| `global.json` | .NET SDK 버전 정책(재현 빌드) |
| `.gitignore` | `.env`·`*.pfx`·`secrets/`·`target/`·`bin/obj/` 등 제외 |

### 2-2. 개발/CI 설정
| 파일 | 목적 |
| --- | --- |
| `.claude/settings.json` | git 일반·cargo·dotnet·읽기전용 자동 허용, 파괴적 명령은 ask. 다른 PC 동일 |
| `.github/workflows/ci.yml` | 코어(mac/win) fmt+clippy+test · cargo-deny 라이선스 게이트 · WinUI 빌드 |
| `.vscode/*` | 권장 확장·rust-analyzer 설정(core 워크스페이스 인식) |
| `scripts/bootstrap.ps1` | 다른 Windows PC에서 winget으로 동일 환경 구성 |

### 2-3. `core/` — Rust 코어(맥/Windows/Linux 빌드)
| 크레이트 | 목적 / 현재 내용 |
| --- | --- |
| `nexa-core` | 코어 공용 타입. `FileKind`(File/Dir/Symlink), `CORE_VERSION`. 단위 테스트 2 |
| `nexa-vfs` | 저장소 추상화. `Provider` trait(현재 `scheme()`만 — M4 전 list/stat/read/watch 계약 확장 예정, 2차 감사 C2), `Entry`(name/kind/size/modified), **로컬 스트리밍 열거 `read_dir_entries`**(점진 산출 Iterator, 엔트리별 Result) + 가시성 필터. 단위 테스트 3 |
| `nexa-tree` | **인라인 트리 + 교차선택 모델(C1)** — 트리를 **가시 노드 평면 스트림**(`VisibleRow`)으로 투영, open/expand/collapse/가시행/경로→NodeId 조회 + **OrderedSet 선택**(교차 폴더). UI 비종속 순수 로직(맥 단위 테스트 11). ADR-0004 [29](29-adr-0004-core-tree-model.md) |
| `nexa-interop` | **cdylib** — C# 호스트가 P/Invoke로 로드. C ABI `nexa_abi_version` + PoC `nexa_poc_add` + **코어 트리 API `nexa_tree_*`**(open/close/가시행/펼침·접힘/선택/경로 조회/**정렬 `nexa_tree_set_sort`**/**타입어헤드 `nexa_tree_find_prefix`**, **ABI v7**). 단위 테스트 7(라운드트립·정렬·타입어헤드 포함). 후속: 이벤트 스트림 |
| `deny.toml` | 허용 라이선스 화이트리스트(MIT/Apache/BSD…), 1st-party 예외(PolyForm) |
| `Cargo.toml` | 워크스페이스 멤버·공통 메타(version/edition/license/authors), release 프로파일(LTO) |

### 2-4. `app/` — WinUI 3 셸(Windows 전용)
| 파일 | 목적 / 현재 내용 |
| --- | --- |
| `Nexa.App.csproj` | WinUI3 앱. `net8.0-windows10.0.22621.0`, `WindowsPackageType=None`(포터블 친화), x64/arm64. **인터롭 타겟**: `BuildNexaInterop`(cargo build) → `CopyNexaInterop`(nexa_interop.dll→출력) |
| `App.xaml(.cs)` | 애플리케이션 객체·Fluent 리소스, 메인 윈도우 생성 |
| `MainWindow.xaml(.cs)` | 메인 윈도우(**955줄**). 듀얼 패널·패널별 탭·계층 경로바·인라인 트리 가상화 소비·네비게이션·키보드·**비동기 디렉터리 로드**(`LoadDirectory` = `Task.Run` 백그라운드 열거+세대 가드, P1). 좌/우 상태·UI는 `PanelView` 2개(`Panel(left)`, B-2a). 순수 로직은 `Nexa.ViewModels`로 이관 중(god object 축소, B-1) |
| `PanelView.cs` | 패널 하나(좌/우)의 상태(Tabs/Active/LoadGen) + UI 참조(Grid/Header/PathBar/TabStrip) 묶음 → `bool left` 이중화 소거(B-2a) |
| `VirtualTreeCollection.cs` | 코어 트리(`nexa-tree`) 가시행 평면 스트림을 **가상화로 소비**하는 `IList`(보이는 행만 지연 실체화·캐시). 백그라운드 `OpenAndExpand`(열거+펼침) + UI 스레드 `AdoptHandle`(핸들 채택·Reset). 선택/캐럿/펼침은 코어 위임 |
| `NativeInterop.cs` | 코어 cdylib P/Invoke 바인딩(`nexa_abi_version`/`nexa_poc_add` + 코어 트리 `nexa_tree_*`, 마샬 은닉 `Tree*` API, `DirItem`). ABI 레이아웃 가드(`VerifyAbi`)·`NexaEntry` 미러. 런타임에 `nexa_interop.dll` 로드 (구 C# 디렉터리 열거 `nexa_dir_*`/`ReadDir`는 C1에서 코어 트리로 대체·제거, E17) |
| `app.manifest` | PerMonitorV2 고DPI, 지원 OS(Win10/11) |

> 맥에서는 **빌드 불가**(WinUI XAML 컴파일러가 Windows 전용) — 상세 [11 §개발OS](11-dev-environment.md).

### 2-5. `docs/` — 설계/결정/기록
- `00~09` 설계(비전·아키텍처·로드맵·기능·트렌드·요구·ADR·플래그십·경쟁조사·플러그인)
- `10~23` 결정·운영(결정기록·개발환경·포터블·라이선스·컨텍스트공유·방법론·**구조(본 문서)**·라이선스인증·빌드&테스트·구현기능현황·UI레이아웃·ADR-0002 파일뷰·ADR-0003 뷰/패널모듈·컬럼시스템)
- `STATUS.md` 현황 요약 · `journal/` 타임스탬프 작업 기록(질문·결정·진행)

## 3. 현재 상태 요약
- 코어: `cargo test` green(**4 crates / 21 tests** + 벤치 1 ignored), fmt·clippy clean. nexa-vfs 스트리밍 열거·필터 + nexa-tree 트리/선택 모델 + interop `nexa_tree_*` C ABI(v5).
- 앱: 듀얼 패널·탭·경로바·인라인 트리 가상화·**비동기 로드** 동작(C# P/Invoke ↔ Rust cdylib) — Windows/CI 빌드 검증, **CI success**.
- 현재 브랜치: `refactor/002-audit`(2차 감사) — 트랙 A 성능 P3(ABI v5)·P1(백그라운드 열거) 완료, 다음 P2. 상세 [STATUS](STATUS.md)·[journal](journal/archive/refactor-002-worklog.md).
