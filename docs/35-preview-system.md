# 35 · 미리보기 시스템 — 표준 표시 + 플러그인 공급자 (BP-2)

> 하단 패널 "미리보기" 탭이 파일 내용을 보여준다. **표시 방식을 표준화**하고 **공급자(provider) 플러그인**으로
> 포맷을 확장한다. 관련: 하단 패널 [BP-1](journal/bottom-panel-worklog는 아카이브) · ADR-0003 [22](22-adr-0003-view-and-panel-modules.md)(IToolPanel) · 플러그인 [09](09-plugin-architecture.md).
> 상태: **텍스트·이미지 구현**, 나머지 포맷은 공급자 추가로 확장(설계).

## 1. 표준 (표시 방식)

- **공급자는 완성된 `FrameworkElement`를 반환**하고, 호스트(`BottomDockView`)는 그것을 미리보기 영역(`PreviewHost`)에
  **얹기만** 한다. 스크롤/맞춤/상호작용은 공급자 요소가 자체 처리(텍스트=TextBox 스크롤, 이미지=Uniform 맞춤).
- **대상 지정**: 호스트가 `PreviewPath`(단일 선택된 파일 경로)를 설정 → 종류=미리보기일 때 렌더. 폴더/다중/무선택은 안내 메시지.
- **비동기·취소**: `CreatePreviewAsync(path, ct)` — 무거운 I/O는 내부 `Task.Run`, 빠른 선택 전환 시 이전 렌더는 `CancellationToken`으로 취소. 로딩 중/미지원/실패는 표준 메시지 블록.

## 2. 인터페이스 (플러그인 확장점)

```csharp
public interface IPreviewProvider
{
    string Name { get; }                                  // 표시 이름(텍스트/이미지…)
    bool CanPreview(string path);                         // 확장자/시그니처로 처리 가능 판정
    Task<FrameworkElement?> CreatePreviewAsync(string path, CancellationToken ct);
}
```

- **레지스트리** `PreviewRegistry`: 등록 순서 역순(나중 등록 우선)으로 첫 `CanPreview` 매치를 사용. **플러그인은 `Register`로 추가**해
  우선권을 가진다(내장 공급자 대체/보강 가능).
- **계약 = 퍼미시브 MIT SDK**(DR-6): `IPreviewProvider`·`PreviewRequest`·`PreviewRegistry`는 [`app/Nexa.Plugins/Preview/`](../app/Nexa.Plugins/)(누구나 플러그인 개발, [36](36-plugin-development.md)).
- **공급자 = 샘플 플러그인**: 텍스트·이미지 공급자는 [`app/Nexa.Plugins.Samples/`](../app/Nexa.Plugins.Samples/)(MIT, SDK만 참조)에 있고 앱이 `App.xaml.cs`에서 등록(dogfooding).
- **크기 상호연동**: 호스트가 미리보기 영역 크기를 `PreviewRequest`로 전달하고, **영역 리사이즈 시 재렌더**한다(이미지 디코드 해상도 등 적응). 텍스트=`ScrollViewer`+`TextBlock`으로 가로/세로 스크롤(WinUI TextBox는 LF 텍스트를 1줄로 렌더하는 문제가 있어 TextBlock 사용).

## 3. 내장 공급자 (구현)

| 공급자 | 대상 | 방식 |
| --- | --- | --- |
| **텍스트** | `.txt/.md/.json/.xml/.csv/.log/.cs/.js/.py/.rs/.yml/…` + 확장자 없는 흔한 텍스트(LICENSE·README·Dockerfile) | 앞부분 ≤256KB를 UTF-8로 읽어 고정폭 읽기전용 TextBox(가로/세로 스크롤). 큰 파일은 "이하 생략" 표시 |
| **이미지** | `.png/.jpg/.jpeg/.gif/.bmp/.webp/.ico/.tif` | `StorageFile`→`BitmapImage.SetSourceAsync`→`Image`(Uniform 맞춤) |

## 4. 후속 포맷 (공급자로 확장 — 목표)

각 포맷은 **새 `IPreviewProvider`를 추가·등록**하면 된다(호스트/표준 불변). 대부분 외부 라이브러리 또는 코어(nexa-core) 지원 필요:

| 포맷 | 공급자(예정) | 필요 요소 |
| --- | --- | --- |
| **압축 파일 내용** | `ArchivePreviewProvider` | zip/7z/rar 목록 표시. **암호 필요 시 입력란**(비밀번호 → 목록/추출). 퍼미시브 zip 라이브러리(코어 Rust `zip`/`sevenz` 또는 C#) |
| **PDF** | `PdfPreviewProvider` | 첫 페이지 렌더(Windows `Windows.Data.Pdf` 또는 렌더러). 페이지 이동 |
| **Office** | `OfficePreviewProvider` | docx/xlsx/pptx → 렌더/변환(라이선스·퍼미시브 검토, 또는 텍스트 추출) |
| 미디어/오디오·비디오 | `MediaPreviewProvider` | MediaPlayerElement |
| Markdown 렌더 | 텍스트 공급자 위 렌더 옵션 | 후속 |

- **암호(압축)**: 공급자가 암호 필요를 감지하면 미리보기 영역에 **암호 입력 UI**를 표시하고, 입력 시 목록/내용을 렌더(공급자 내부 상태). 표준 인터페이스는 그대로(요소만 다름).
- **플러그인(docs/09)**: 장기적으로 미리보기 공급자를 **플러그인 기여점**으로 노출 — T1 인프로세스(WASM/WIT) 또는 T2 아웃오브프로세스(RPC)로 서드파티 포맷 지원. 위험 디코딩은 **격리 워커**(NFR-R3).

## 4-1. 메타데이터 정보 표시(정보 탭) — `nexa-meta` 공용 (사용자 요청 2026-07-08)

> "자체 Meta 포맷이 있는 파일들의 파일 정보 표시 개선" — 정보(Info) 탭이 파일 내부 임베디드 메타를 속성 표로 보여준다.

- **단일 추출기 `nexa-meta`(Rust)**: 사진(EXIF/IPTC/XMP)·동영상(MP4 atom)·오디오(ID3/Vorbis)·문서(PDF/OOXML)를
  키-값 딕셔너리로 반환(퍼미시브 순수 Rust 크레이트 — `kamadak-exif`·`mp4`·`lofty`·`lopdf`·`zip`+`quick-xml`).
  **일괄 이름변경의 `{meta.KEY}` 변수와 완전 공용**(상세 [25 §5](25-bulk-rename.md)).
- **정보 탭 렌더**: 기본 속성(크기·날짜·종류) + 포맷별 메타 섹션(있는 키만). 지연/부분 파싱·mtime 캐시.
- **안전**: 신뢰 못할 파일 디코딩은 격리(NFR-R3). 없는 키는 표시 생략(리네임은 널 정책).

## 5. 성능·안정 (NFR)

- 대용량 텍스트=앞부분만, 이미지=디코드 후 스트림 해제. 빠른 스크롤/선택 시 이전 렌더 취소로 낭비 제거.
- 위험/신뢰불가 포맷(장기): 아웃오브프로세스 격리(크래시가 앱을 죽이지 않게). 미리보기는 **선택 시 지연 로드**.
