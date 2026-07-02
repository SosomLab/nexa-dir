# 30 · 용어집 (Glossary) — 인터롭/배관 용어

> Rust 코어 ↔ WinUI(C#) 경계("배관")에서 쓰는 용어를 한글·영어로 정리한다.
> 관련: [01 아키텍처](01-architecture.md)·[06 ADR-0001 스택](06-adr-0001-stack.md)·[29 ADR-0004 트리모델](29-adr-0004-core-tree-model.md).

## 큰 그림 — "배관(Plumbing)"

**배관 = 데이터가 코어(Rust)에서 화면(C#)까지 흐르도록 계층 간 통로를 까는 일.**
물이 흐르려면 파이프를 먼저 이어야 하듯, 기능이 동작하려면 계층 연결을 먼저 만든다.
"수도꼭지(UI)"를 틀기 전 단계가 배관이며, 실제 화면 연결은 **배선(wiring)** 이라 부른다.

```
Rust 코어 ──(C ABI)──> DLL ──(P/Invoke)──> C# 관리형 코드 ──(배선)──> 화면
  물탱크        파이프 규격   파이프      수도관 연결부              수도꼭지
```

## 1. 계층 · 경계

| 한글 | English | 뜻 (이 프로젝트) |
| --- | --- | --- |
| 코어 | Core | 성능 핵심 로직(Rust). `nexa-core`/`nexa-vfs`/`nexa-tree` |
| 인터롭 | Interop (interoperability) | 서로 다른 언어(Rust↔C#)가 대화하는 경계 계층. `nexa-interop` |
| FFI | Foreign Function Interface | 한 언어에서 다른 언어 함수를 호출하는 방식 |
| C ABI | C Application Binary Interface | 함수/구조체의 **바이너리 수준** 배치 약속. C 규약을 공용어로 |
| cdylib | C-compatible dynamic library | Rust를 C에서 부를 수 있는 `.dll` 형태로 빌드한 결과 |
| DLL | Dynamic Link Library | 실행 시 로드되는 라이브러리. `nexa_interop.dll` |
| P/Invoke | Platform Invoke | C#이 네이티브 DLL 함수를 부르는 .NET 메커니즘(`[DllImport]`) |
| 관리형 | Managed (code) | .NET(GC 관리) 코드 = C# 쪽 |
| 네이티브 | Native (code) | OS/기계어에 가까운 코드 = Rust/DLL 쪽 |

## 2. 데이터 전달

| 한글 | English | 뜻 |
| --- | --- | --- |
| 마샬링 | Marshalling | 관리형↔네이티브 사이 데이터를 상대가 이해할 형태로 **변환·복사** |
| 핸들 | Handle | 네이티브가 소유한 객체를 가리키는 **불투명 표**(여닫기만). `TreeHandle` |
| 포인터 | Pointer | 메모리 주소값. C#에선 `IntPtr` |
| 불투명 | Opaque | 내부 구조를 감춘 것(핸들처럼 손잡이만 노출) |
| 구조체 미러 | Struct mirror | 같은 메모리 배치를 Rust·C# 양쪽에 동일 정의한 쌍. `NexaRow`↔`NexaRow` |
| `#[repr(C)]` / `StructLayout` | — | 구조체를 C 규약대로 배치하라는 지시(양쪽 어긋남 방지) |
| 레이아웃 | (memory) Layout | 필드가 메모리에 놓이는 순서·패딩(정렬). 8→4→1바이트 순 배치 |
| 수명 규약 | Lifetime contract | "이 포인터는 다음 호출 전까지만 유효" 같은 유효기간 약속 |
| ABI 버전 | ABI version | DLL·앱이 같은 규약인지 확인하는 번호(`nexa_abi_version`). 불일치 시 거부 |
| 레이아웃 가드 | Layout guard | 구조체 크기를 양쪽 실행 중 대조(`CheckLayout`/`nexa_*_size`) |

## 3. 트리 · 데이터 모델

| 한글 | English | 뜻 |
| --- | --- | --- |
| 가시 노드 평면 스트림 | Visible-node flat stream | 트리를 "보이는 행들의 1차원 목록"으로 펼침. `VisibleRow` |
| 지연 로딩 | Lazy loading | 필요할 때(펼칠 때/보일 때) 비로소 읽음(전량 선(先)읽기 안 함) |
| 가상화 | Virtualization | **화면에 보이는 행만** 실제 생성·렌더(10만 개여도 수십 개만 실체화) |
| diff / 변경 구간 | diff / delta (range change) | 펼침·접힘으로 바뀐 부분만 통지. `RangeChange {start, removed, inserted}` |
| NodeId | Node identifier | 트리 노드의 안정적 고유 번호 |
| OrderedSet | Ordered set | 중복 없이 **삽입 순서를 보존**하는 집합(교차 선택 순서용) |
| 캐럿 | Caret | 키보드 현재 위치(선택과 별개인 포커스 표시) |
| 앵커 | Anchor | Shift 범위 선택의 기준점 |
| RAII / IDisposable | — | 자원(핸들)을 열면 반드시 닫도록 묶는 패턴(`Close()`/`Dispose()`) |

## 4. 작업 흐름 용어

| 한글 | English | 뜻 |
| --- | --- | --- |
| 배선 | Wiring | 완성된 부품(배관)을 실제 UI(MainWindow)에 연결하는 일 |
| 슬라이스 | (vertical) Slice | 세로로 얇게 자른 작업 단위(≈1 커밋). [15](15-dev-methodology.md) |
| 초안→확장 | Draft → Extend | 최소 초안 커밋 후 점진 확장(방법론) |
| 게이트 | (CI) Gate | 통과해야 하는 자동 검사(fmt/clippy/test/cargo-deny/app build) |

> 한 문장: **배관**(C ABI→P/Invoke→관리형 클라이언트→가상화 컬렉션)으로 코어 데이터를 안전하게(버전·레이아웃 검증) 꺼내 쓸 통로를 잇고, **배선**으로 그 통로를 화면에 연결한다.
