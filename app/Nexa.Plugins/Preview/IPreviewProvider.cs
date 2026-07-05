using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace Nexa.Plugins.Preview;

/// <summary>
/// 미리보기 공급자(플러그인 SDK 계약) — 파일 하나를 하단 미리보기 영역에 표시할 <see cref="FrameworkElement"/>로 만든다.
/// <para><b>표시 방식 표준</b>: 공급자는 완성된 UI 요소(스크롤/맞춤 자체 처리)를 반환하고, 호스트는 그것을 얹기만 한다.
/// 새 포맷(압축/PDF/Office 등)은 <see cref="IPreviewProvider"/>를 구현·등록해 확장한다.</para>
/// 라이선스: 이 SDK는 퍼미시브 MIT — 누구나 플러그인 개발·배포 가능(DR-6).
/// </summary>
public interface IPreviewProvider
{
    /// <summary>표시용 이름(예: 텍스트/이미지).</summary>
    string Name { get; }

    /// <summary>이 공급자가 <paramref name="path"/>를 미리볼 수 있는가(확장자/시그니처 등). 예외를 던지지 말 것.</summary>
    bool CanPreview(string path);

    /// <summary>미리보기 요소를 만든다. <b>UI 스레드</b>에서 호출(요소 생성). 무거운 I/O는 내부에서 <c>Task.Run</c>으로 오프로드.
    /// 빠른 선택 전환에 대비해 <paramref name="ct"/>를 존중(취소 시 중단).
    /// <para><see cref="PreviewRequest"/>로 <b>미리보기 영역 크기</b>를 받아 적응할 수 있다(예: 이미지 디코드 해상도).
    /// 영역이 리사이즈되면 호스트가 다시 호출한다(크기 상호연동).</para></summary>
    Task<FrameworkElement?> CreatePreviewAsync(PreviewRequest request, CancellationToken ct);
}

/// <summary>미리보기 요청 — 대상 경로 + <b>미리보기 영역 크기</b>(px, 0=미확정). 공급자가 크기에 맞춰 렌더할 수 있게 한다.</summary>
public sealed class PreviewRequest
{
    public PreviewRequest(string path, double availableWidth, double availableHeight)
    {
        Path = path;
        AvailableWidth = availableWidth;
        AvailableHeight = availableHeight;
    }

    /// <summary>미리볼 파일 경로.</summary>
    public string Path { get; }

    /// <summary>미리보기 영역 가용 폭(px). 0이면 아직 확정되지 않음.</summary>
    public double AvailableWidth { get; }

    /// <summary>미리보기 영역 가용 높이(px). 0이면 아직 확정되지 않음.</summary>
    public double AvailableHeight { get; }
}

/// <summary>
/// 미리보기 공급자 레지스트리 — 등록 순서 역순(나중 등록 우선)으로 첫 <see cref="IPreviewProvider.CanPreview"/> 매치를 사용.
/// 호스트가 내장 공급자를 등록하고, <b>플러그인은 <see cref="Register"/>로 추가</b>해 우선권을 갖는다(내장 대체/보강 가능).
/// </summary>
public static class PreviewRegistry
{
    private static readonly List<IPreviewProvider> Providers = new();

    /// <summary>공급자 등록(맨 앞에 삽입 → 나중 등록/플러그인이 우선).</summary>
    public static void Register(IPreviewProvider provider) => Providers.Insert(0, provider);

    /// <summary><paramref name="path"/>를 미리볼 첫 공급자(없으면 null). 판정 예외는 격리하고 다음 공급자로.</summary>
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
                // 격리
            }
        }
        return null;
    }

    /// <summary>등록된 공급자 목록(진단/설정용).</summary>
    public static IReadOnlyList<IPreviewProvider> All => Providers;
}
