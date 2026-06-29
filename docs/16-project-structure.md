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
│  ├─ extensions.json      # 권장 확장(rust-analyzer, C# Dev Kit 등)
│  └─ settings.json        # rust-analyzer linkedProjects=core 등
├─ scripts/
│  └─ bootstrap.ps1        # Windows 개발환경 설치(winget) — 재현성
├─ core/                   # ── Rust 코어 워크스페이스(핫패스) ──
│  ├─ Cargo.toml           # 워크스페이스 정의(resolver 2, release LTO)
│  ├─ Cargo.lock           # 의존성 잠금(재현 빌드 — 커밋)
│  ├─ rust-toolchain.toml  # Rust stable + rustfmt/clippy 고정
│  ├─ deny.toml            # cargo-deny 라이선스 게이트(퍼미시브 온리)
│  └─ crates/
│     ├─ nexa-core/        # 공용 타입(FileKind, CORE_VERSION)
│     ├─ nexa-vfs/         # 가상 파일시스템 추상화(Provider, Entry) — 스텁
│     └─ nexa-interop/     # C ABI cdylib(nexa_abi_version) — C# 인터롭 표면
├─ app/                    # ── WinUI 3 UI 셸(Windows 전용) ──
│  ├─ README.md            # 앱 빌드 안내(Windows)
│  └─ Nexa.App/
│     ├─ Nexa.App.csproj   # WinUI3, net8.0-windows, 비패키지(포터블 친화)
│     ├─ App.xaml(.cs)     # 앱 진입점·리소스
│     ├─ MainWindow.xaml(.cs) # 메인 윈도우(현재 빈 셸)
│     └─ app.manifest      # 고DPI(PerMonitorV2)·지원 OS
└─ docs/                   # 설계·결정·작업기록 (00~16 + journal)
```

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
| `nexa-vfs` | 저장소 추상화. `Provider` trait, `Entry` 구조체(스텁). 후속: 로컬 스트리밍 열거 |
| `nexa-interop` | **cdylib** — C# 호스트가 P/Invoke로 로드. `nexa_abi_version()` C ABI. 후속: 핸들 API |
| `deny.toml` | 허용 라이선스 화이트리스트(MIT/Apache/BSD…), 1st-party 예외(PolyForm) |
| `Cargo.toml` | 워크스페이스 멤버·공통 메타(version/edition/license/authors), release 프로파일(LTO) |

### 2-4. `app/` — WinUI 3 셸(Windows 전용)
| 파일 | 목적 / 현재 내용 |
| --- | --- |
| `Nexa.App.csproj` | WinUI3 앱. `net8.0-windows10.0.19041.0`, `WindowsPackageType=None`(포터블 친화), x64/arm64 |
| `App.xaml(.cs)` | 애플리케이션 객체·Fluent 리소스, 메인 윈도우 생성 |
| `MainWindow.xaml(.cs)` | 메인 윈도우(현재 "Nexa Dir" 라벨만). 후속: 경로바·듀얼패널·트리 |
| `app.manifest` | PerMonitorV2 고DPI, 지원 OS(Win10/11) |

> 맥에서는 **빌드 불가**(WinUI XAML 컴파일러가 Windows 전용) — 상세 [11 §개발OS](11-dev-environment.md).

### 2-5. `docs/` — 설계/결정/기록
- `00~09` 설계(비전·아키텍처·로드맵·기능·트렌드·요구·ADR·플래그십·경쟁조사·플러그인)
- `10~16` 결정·운영(결정기록·개발환경·포터블·라이선스·컨텍스트공유·방법론·**구조(본 문서)**)
- `journal/` 타임스탬프 작업 기록(질문·결정·진행)

## 3. 현재 상태 요약
- 코어: `cargo test` green(4 crates / 4 tests), fmt·clippy clean.
- 앱: 스켈레톤(빈 윈도우) — Windows/CI에서 빌드 검증.
- 다음 단위: 인터롭 PoC → 로컬 스트리밍 열거 → 가상화 렌더 → 네비게이션 (docs/15 §7).
