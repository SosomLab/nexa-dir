using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace Nexa.App.Preview;

/// <summary>
/// 미리보기 공급자(표준 인터페이스, docs/35) — 파일 하나를 하단 미리보기 영역에 표시할 <see cref="FrameworkElement"/>로 만든다.
/// 표시 방식 표준화: 공급자는 <b>완성된 UI 요소</b>(스크롤/맞춤 자체 처리)를 반환하고, 호스트(<c>BottomDockView</c>)는
/// 그것을 미리보기 영역에 균일하게 얹기만 한다. 새 포맷(압축/PDF/Office 등)은 공급자를 추가·등록해 확장(플러그인).
/// </summary>
public interface IPreviewProvider
{
    /// <summary>표시용 이름(예: 텍스트/이미지).</summary>
    string Name { get; }

    /// <summary>이 공급자가 <paramref name="path"/>를 미리볼 수 있는가(확장자/시그니처 등).</summary>
    bool CanPreview(string path);

    /// <summary>미리보기 요소를 만든다. UI 스레드에서 호출(요소 생성). 무거운 I/O는 내부에서 <c>Task.Run</c>. 취소 지원.</summary>
    Task<FrameworkElement?> CreatePreviewAsync(string path, CancellationToken ct);
}

/// <summary>
/// 미리보기 공급자 레지스트리 — 등록 순서대로 첫 <see cref="IPreviewProvider.CanPreview"/> 매치를 사용한다.
/// 플러그인은 <see cref="Register"/>로 <b>앞에</b> 추가해 우선권을 갖는다(docs/35 플러그인 확장점).
/// </summary>
public static class PreviewRegistry
{
    private static readonly List<IPreviewProvider> Providers = new()
    {
        new ImagePreviewProvider(),
        new TextPreviewProvider(),
        // 후속: ArchivePreviewProvider(암호 입력)·PdfPreviewProvider·OfficePreviewProvider …(docs/35)
    };

    /// <summary><paramref name="path"/>를 미리볼 첫 공급자(없으면 null).</summary>
    public static IPreviewProvider? Find(string path)
    {
        foreach (var p in Providers)
        {
            try
            {
                if (p.CanPreview(path))
                {
                    return p;
                }
            }
            catch
            {
                // 공급자 판정 실패는 격리(다음 공급자로)
            }
        }
        return null;
    }

    /// <summary>플러그인/추가 공급자 등록(맨 앞 = 우선).</summary>
    public static void Register(IPreviewProvider provider) => Providers.Insert(0, provider);
}
