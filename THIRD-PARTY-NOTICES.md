# Third-Party Notices

Nexa Dir는 다음 오픈소스에 의존합니다. 각 구성요소의 라이선스는 퍼미시브(MIT/Apache/BSD 등)이며,
상업 배포와 호환됩니다(정책: [docs/13-licensing.md](docs/13-licensing.md)).

> 이 파일은 의존성이 추가될 때 갱신한다. CI에서 `cargo deny check licenses`로 퍼미시브 정책을 강제한다.
> (자동 생성 도구: `cargo about` / `cargo bundle-licenses` 도입 예정)

## Rust (core/)

| 크레이트 | 라이선스 | 비고 |
| --- | --- | --- |
| (직접 의존성 없음 — 스캐폴딩 단계) | — | 표준 라이브러리만 사용 |

## .NET / WinUI (app/)

| 패키지 | 라이선스 | 비고 |
| --- | --- | --- |
| Windows App SDK (WinUI 3) | MIT | |
| .NET Runtime | MIT | |
| CommunityToolkit.WinUI.Controls.Sizers (GridSplitter) | MIT | 영역 크기 조절 |

---

향후 추가 예정(예): tantivy(MIT), image(MIT/Apache-2.0), notify(MIT/CC0), zip(MIT),
ssh2/libssh2(BSD), aws-sdk(Apache-2.0), wasmtime(Apache-2.0 WITH LLVM-exception), mlua(MIT).
