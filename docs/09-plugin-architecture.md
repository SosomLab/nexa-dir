# 09 · 플러그인 아키텍처 (스크립트 확장: Python/Node/Lua/WASM)

> 외부 개발자가 **스크립트로 기능을 확장**하고 **쉽게 개발·배포**할 수 있는 구조 설계.
> 두 질문에 답한다: **(A) 보통 무엇을 확장 기능으로 제공하나**, **(B) 개발·확장·배포가 쉬운 기술 구조 추천**.
> 관련 NFR: 오류 격리(R3)·무간섭(N1)·저메모리(M2) → 플러그인은 **샌드박스·격리**가 전제.

---

## A. 무엇을 확장하나 — 확장 포인트(Contribution Points)

> VS Code / Directory Opus / Total Commander / XYplorer / Obsidian 생태계에서 공통적으로 제공하는 확장 영역.

| 분류 | 확장 포인트 | 예시 |
| --- | --- | --- |
| **명령/액션** | 명령 등록(메뉴·툴바·명령 팔레트·단축키) | "WebP로 변환", "여기서 빌드" |
| **컨텍스트 메뉴** | 우클릭 항목 추가(선택 기반) | "해시 계산", "S3 업로드" |
| **커스텀 컬럼** | 파일별 계산 값 컬럼 | 이미지 해상도, EXIF 날짜, git 상태 |
| **미리보기/뷰어** | 새 포맷 프리뷰 | RAW, 3D, 마크다운 렌더 |
| **썸네일 공급자** | 썸네일 생성 | CAD/폰트/특수 포맷 |
| **VFS 공급자(프로토콜)** | 새 저장소 마운트 | WebDAV·MTP·DB·게임 패키지·아카이브 |
| **파일 작업/배치** | 일괄 변환·정리 | 대량 리네임 규칙, 변환 파이프 |
| **검색 공급자/필터** | 검색 백엔드·필터 문법 | 코드 심볼 검색, 사내 DMS |
| **사이드 패널/커스텀 뷰** | 도구 패널 | git 패널, 메타 편집기 |
| **자동화/규칙(watch)** | 이벤트 트리거 | "다운로드에 PDF 들어오면 분류" |
| **메타데이터 추출/태깅** | 인덱싱 보강 | 자막·문서 본문·AI 태그 |
| **통합/내보내기** | 외부 서비스 연동 | 메신저 공유, 티켓 첨부 |
| **AI 액션** | 코어 AI 호출 | 요약·이름 제안·분류 |
| **테마/UI** | 색·아이콘·레이아웃 | 다크 변형, 아이콘 팩 |

> 위 중 **명령·컨텍스트 메뉴·커스텀 컬럼·미리보기·VFS 공급자·자동화 규칙**이 수요가 가장 크다 → 1차 공개 대상.

---

## B. 권장 기술 구조

### B-1. 핵심 설계 원칙
1. **인프로세스 금지(기본)** — 플러그인은 메인 앱과 **격리**되어, 크래시·누수·폭주가 앱/다른 프로그램에
   영향 없음(NFR-R3/N1/M2). 권한 없는 동작 불가.
2. **선언적 매니페스트 + 명령형 API** — 간단한 확장은 코드 거의 없이 manifest만으로.
3. **언어 중립 ABI** — 호스트 API를 한 번 정의하고 여러 언어가 동일 인터페이스 사용.
4. **역량(Capability) 기반 권한** — 플러그인이 필요한 권한을 선언 → 사용자 동의 → 경계에서 강제.

### B-2. 실행 모델 — 2-tier (성능 vs 생태계 균형)

| Tier | 런타임 | 용도 | 격리/성능 |
| --- | --- | --- | --- |
| **T1 인프로세스 샌드박스** | **WASM(Component Model)** / **Lua(mlua)** | 핫패스 경량 로직(커스텀 컬럼·필터·리네임 규칙·간단 변환) | 샌드박스 + fuel/메모리 캡, 저지연 |
| **T2 아웃오브프로세스** | **Python / Node / 임의 실행파일** | 풍부한 플러그인(VFS·뷰어·통합·AI), 네이티브 생태계 필요 | 별도 프로세스 + RPC, 완전 크래시 격리 |

- **T1(WASM/WIT)** = 전략적 코어. **WIT로 호스트 API 1회 정의** → Rust/C/Go/JS/Python(componentize)
  등이 컴파일하여 **단일 `.wasm`** 산출. 샌드박스·결정적·이식성·**배포 단순**(파일 1개)에서 최상.
- **T2(JSON-RPC over stdio/소켓)** = VS Code/LSP식. 플러그인이 자기 프로세스에서 실행, 호스트와
  **JSON-RPC(또는 MessagePack)** 통신. Python/Node의 방대한 패키지 그대로 사용. 크래시·행이 앱과 무관.

> 권장: **WASM을 1급 + 아웃오브프로세스 RPC를 병행**. "가볍고 안전·배포 쉬움"은 WASM,
> "기존 생태계·빠른 프로토타이핑"은 Python/Node 프로세스.

### B-3. 플러그인 패키지 구조 (개발 쉬움 핵심)

```
my-plugin/
├─ nexa.plugin.toml        # 매니페스트: id, name, version, engine, contributes, permissions, activation
├─ src/ ...                # python / node / lua / wasm 엔트리
├─ icon.png
└─ README.md
```

`nexa.plugin.toml` 예:
```toml
id = "acme.webp-convert"
name = "WebP 변환"
version = "1.0.0"
engine = "wasm"            # wasm | python | node | lua
entry = "plugin.wasm"
[contributes]
commands = [{ id = "webp.convert", title = "WebP로 변환" }]
menus.context = [{ command = "webp.convert", when = "selection.images" }]
columns = [{ id = "dim", title = "해상도", provider = "webp.dim" }]
[activation]
events = ["onContextMenu:image", "onCommand:webp.convert"]
[permissions]
fs = ["read", "write:selection"]   # net, exec, clipboard, ai 등 선언
```

- **선언적 contribution points**로 메뉴/컬럼/명령은 코드 없이 등록 → 로직만 엔트리에 구현.
- **activation events**로 필요할 때만 로드(상주 메모리 절감, NFR-M2).

### B-4. 언어별 SDK (이식·친화성)

| 언어 | 배포 | 사용감 |
| --- | --- | --- |
| Python | `pip install nexa-plugin` | `@command`, `@column` 데코레이터로 RPC 은닉 |
| Node/TS | `npm i @nexa/plugin` | 타입 정의 + async API |
| Lua | 번들 모듈 | 경량 훅 함수 |
| WASM(any) | WIT 바인딩(`wit-bindgen`) | 타입 안전, 단일 .wasm |

SDK가 RPC/ABI를 감싸 **호스트 함수 호출 = 일반 함수 호출**처럼 보이게 한다.

### B-5. 권한/리소스 모델 (안전 + 상주 규율)
- **Capability 선언 → 사용자 동의 → 경계 강제**: `fs:read/write`, `net`, `exec`, `clipboard`, `ai`, `vfs`.
- **리소스 한도**: T1은 fuel/메모리 캡, T2는 프로세스 CPU·메모리·타임아웃, 폭주 시 강제 종료·재시작.
- **서명/신뢰**: 배포 번들 서명, 미서명은 경고·제한 모드.

---

## C. 외부 개발자 경험 (DX)

- **스캐폴딩 CLI**: `nexa plugin create --lang python|node|lua|wasm` → 동작하는 템플릿.
- **핫 리로드(dev 모드)**: 저장 시 즉시 반영, 콘솔/devtools 로그.
- **타입드 API + 문서 + 샘플 갤러리**: WIT/TS/Python 타입, 예제 모음.
- **테스트 하니스**: 가짜 호스트로 단위 테스트, CI 친화.
- **호환성 메타**: `apiVersion` 범위로 호스트-플러그인 버전 매칭.

## D. 배포 (Distribution)

- **번들**: 매니페스트 + 코드 zip(서명) = `.nexaext`.
- **레지스트리/마켓플레이스**: 검색·설치·자동 업데이트.
  - **부트스트랩**: **Git 기반 레지스트리**(매니페스트 인덱스 repo, Obsidian 커뮤니티 플러그인식) →
    초기 비용 최소, 이후 전용 마켓으로 확장.
- **설치 경로**: 레지스트리 / URL / 로컬 폴더(드래그). per-user 설치, 샌드박스 디렉터리.
- **업데이트/롤백**: 버전 핀·자동 업데이트·비활성/제거 안전.

---

## E. 우리 아키텍처 매핑

- **`nexa-plugin` 크레이트(Rust 코어)** 가 플러그인 호스트: 생명주기, capability 강제,
  **WASM 런타임(wasmtime)**, **Lua(mlua)**, 아웃오브프로세스 언어 호스트(Python/Node) 스폰·RPC.
- 코어가 이미 VFS/ops/index/preview/ai를 소유 → 플러그인은 **코어 경계의 trait 구현**으로 자연 연결
  (예: VFS 공급자 플러그인 = `Provider` trait의 원격 구현을 RPC로 위임).
- **UI 기여**(메뉴/패널/컬럼)는 기존 인터롭 이벤트 스트림으로 WinUI 계층에 전달 → 단일 액션 레지스트리
  (컨텍스트 메뉴·명령 팔레트·런처와 공유, [03 §3-J/§3-K]).

## F. 단계적 도입 (로드맵)

- **M6-α (MVP 플러그인 API)**: 매니페스트 + 명령/컨텍스트 메뉴/커스텀 컬럼, **WASM(T1) + Python(T2)** 우선,
  로컬 폴더 설치, capability 기본 3종(fs/exec/net).
- **M6-β**: 미리보기·VFS 공급자·자동화 규칙 확장 포인트, Node/Lua SDK, 핫리로드/스캐폴딩 CLI.
- **M6-γ**: Git 레지스트리 → 마켓플레이스, 서명/신뢰, 업데이트 채널, 샌드박스 강화.

## G. 권장 요약

> **WIT로 정의한 호스트 API + WASM Component(1급, 안전·단일파일 배포)** 를 코어로,
> **Python/Node 아웃오브프로세스 RPC(생태계·친화성)** 를 병행. **선언적 매니페스트 + contribution points
> + 언어별 SDK + Git 기반 레지스트리**로 외부 개발자가 *쉽게 만들고 안전하게 배포*하게 한다.
> 모든 플러그인은 **격리·권한·리소스 한도** 아래 동작해 상주 안정성(R3/N1/M2)을 보장.
