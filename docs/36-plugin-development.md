# 36 · 플러그인 개발 매뉴얼 (Plugin Development Guide)

> **누구나 플러그인을 만들 수 있습니다.** 플러그인 SDK(`Nexa.Plugins`)와 예제는 **퍼미시브 MIT 라이선스**(DR-6)로,
> 개인·상업 목적 모두 자유롭게 개발·배포·판매할 수 있습니다. (앱 본체는 별도 라이선스 — 이 SDK와 무관.)
> 관련: 미리보기 시스템 [35](35-preview-system.md) · 플러그인 아키텍처(장기) [09](09-plugin-architecture.md) · 라이선스 [13 §7](13-licensing.md).

---

## 0. 지금 할 수 있는 것 (현재 범위)

- **미리보기 공급자(Preview Provider)**: 하단 패널 "미리보기"에 새 파일 포맷을 표시(텍스트·이미지가 내장, 여러분이 압축/PDF/CSV 등 추가).
- **구현 방식**: `Nexa.Plugins`의 `IPreviewProvider`를 구현하고 `PreviewRegistry`에 등록.
- **로딩(현재)**: 컴파일된 어셈블리로 참조·등록(인프로세스). **동적 로딩(.dll/.wasm 배포)** 은 로드맵(M6, [09](09-plugin-architecture.md)) — 계약(`IPreviewProvider`)은 그때도 동일하게 유지됩니다.
- 향후 기여점: 명령·컨텍스트 메뉴·커스텀 컬럼·VFS 공급자 등([09](09-plugin-architecture.md)). 현재 문서는 **미리보기**를 다룹니다.
- **★ 바로 쓸 수 있는 샘플 소스**: [`app/Nexa.Plugins.Samples/`](../app/Nexa.Plugins.Samples/) — **텍스트**([TextPreviewProvider.cs](../app/Nexa.Plugins.Samples/TextPreviewProvider.cs))·**이미지**([ImagePreviewProvider.cs](../app/Nexa.Plugins.Samples/ImagePreviewProvider.cs)) 공급자의 **완성 소스**(MIT). `Nexa.Plugins`(SDK)만 참조하는 별도 프로젝트 = **여러분의 플러그인과 동일한 구조**. 이 샘플이 앱의 실제 내장 미리보기로도 등록됩니다(dogfooding). 복사·개조해서 시작하세요.

## 1. 준비

1. .NET 8 SDK + Windows(WinUI 3). 개발 환경 [11](11-dev-environment.md).
2. `Nexa.Plugins`(SosomLab.Nexa.Plugins) 참조 — 소스 트리에서는 `ProjectReference`, 배포 시 NuGet 패키지.

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Nexa.Plugins/Nexa.Plugins.csproj" />
  <!-- 또는:  <PackageReference Include="SosomLab.Nexa.Plugins" Version="..." /> -->
</ItemGroup>
```

## 2. 계약 (IPreviewProvider)

```csharp
namespace Nexa.Plugins.Preview;

public interface IPreviewProvider
{
    string Name { get; }                                   // 표시 이름
    bool CanPreview(string path);                          // 처리 가능 판정(예외 던지지 말 것)
    Task<FrameworkElement?> CreatePreviewAsync(PreviewRequest request, CancellationToken ct);
}

public sealed class PreviewRequest        // 대상 + 미리보기 영역 크기(px, 0=미확정)
{
    public string Path { get; }
    public double AvailableWidth { get; }
    public double AvailableHeight { get; }
}
```

**표시 표준**: 공급자는 **완성된 `FrameworkElement`** 를 반환합니다. 호스트는 그 요소를 미리보기 영역에 얹기만 하므로,
스크롤·맞춤·상호작용은 여러분의 요소가 스스로 처리합니다(텍스트=`ScrollViewer`+`TextBlock`으로 가로/세로 스크롤, 이미지=Uniform Image 등).

**크기 상호연동**: `PreviewRequest.AvailableWidth/Height`로 **미리보기 영역 크기**를 받아 적응할 수 있습니다(예: 이미지 디코드 해상도).
영역이 리사이즈되면 호스트가 `CreatePreviewAsync`를 **다시 호출**하므로, 크기에 맞춰 더 선명하게/다르게 렌더할 수 있습니다.

## 3. 5분 예제 — CSV를 표로 미리보기

내장 텍스트 공급자는 `.csv`를 평문으로 보여줍니다. 아래 플러그인은 이를 **표(Grid)** 로 대체합니다(나중에 등록 → 우선권).

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexa.Plugins.Preview;

public sealed class CsvPreviewProvider : IPreviewProvider
{
    public string Name => "CSV 표";

    public bool CanPreview(string path) =>
        !Directory.Exists(path) &&
        Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<FrameworkElement?> CreatePreviewAsync(PreviewRequest request, CancellationToken ct)
    {
        // 무거운 I/O는 백그라운드로(UI 무블록). 앞 200줄만.  (request.AvailableWidth/Height로 영역 크기도 활용 가능)
        string[][] rows = await Task.Run(() =>
            File.ReadLines(request.Path).Take(200)
                .Select(line => line.Split(','))
                .ToArray(), ct);
        ct.ThrowIfCancellationRequested();

        var grid = new Grid { Padding = new Thickness(4) };
        int cols = rows.Length == 0 ? 0 : rows.Max(r => r.Length);
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < rows.Length; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < rows[r].Length; c++)
            {
                var cell = new TextBlock { Text = rows[r][c], FontSize = 12, Padding = new Thickness(4, 1, 8, 1) };
                if (r == 0) cell.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;   // 헤더
                Grid.SetRow(cell, r); Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        // 넘칠 수 있으니 스크롤로 감싸 반환(표시 표준: 완성된 요소).
        return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
```

## 4. 등록

```csharp
using Nexa.Plugins.Preview;

// 앱 시작 시(또는 플러그인 초기화 시) 한 번:
PreviewRegistry.Register(new CsvPreviewProvider());
```

- **우선순위**: `Register`는 **맨 앞에 삽입** → 나중에 등록한 공급자(=플러그인)가 내장보다 먼저 매치됩니다. 즉 **내장 동작을 덮어쓸 수 있습니다**(위 예제가 `.csv` 텍스트 미리보기를 표로 대체).
- 내장 공급자(텍스트·이미지)는 앱이 시작 시 등록합니다.

## 5. 지침 (좋은 플러그인)

- **`CanPreview`는 가볍고 예외 없이**(확장자/시그니처만). 무거운 판정 금지.
- **비동기·취소**: I/O는 `Task.Run`으로 오프로드, `CancellationToken`을 자주 확인(`ct.ThrowIfCancellationRequested()`) — 사용자가 파일을 빠르게 바꾸면 이전 렌더가 취소됩니다.
- **크기 상한**: 대용량은 앞부분만/축소 디코드. 미리보기는 "빠르게 훑기"가 목적입니다.
- **오류 격리**: 예외는 삼키거나 의미 있는 메시지로. 미리보기 실패가 앱을 흔들면 안 됩니다(호스트도 격리하지만 공급자도 방어).
- **암호가 필요한 포맷(압축 등)**: 반환 요소 안에 **암호 입력 UI**를 두고, 입력 시 목록/내용을 렌더하세요(계약은 그대로, 요소만 상태를 가짐).
- **UI 스레드**: `CreatePreviewAsync`는 UI 스레드에서 호출됩니다(요소 생성). 백그라운드 작업 후 UI 요소 생성은 `await` 뒤(다시 UI 컨텍스트)에서.

## 6. 배포 (현재/로드맵)

- **현재**: 어셈블리를 앱과 함께 빌드/참조하고 시작 시 `Register`. (내부·사내·포크 배포에 적합.)
- **로드맵(M6, [09](09-plugin-architecture.md))**: `.nexaext` 패키지 + 매니페스트로 **동적 로딩**(별도 빌드 불요), T1 인프로세스(WASM/WIT)·T2 아웃오브프로세스(RPC Python/Node), Capability 권한·서명, 레지스트리. 이때도 `IPreviewProvider` 계약은 유지되어 지금 만든 공급자를 그대로 재사용할 수 있게 하는 것이 목표입니다.

## 7. 라이선스 (중요)

- 이 SDK(`Nexa.Plugins`)와 예제는 **MIT**(DR-6). **여러분의 플러그인은 원하는 라이선스로** 배포·판매할 수 있습니다(오픈소스든 상용이든).
- 앱 본체(Nexa Dir)는 PolyForm Noncommercial(DR-5)이며, 플러그인 개발/배포에는 **영향을 주지 않습니다**.
