using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexa.Plugins.Preview;
using Windows.Storage;

namespace Nexa.Plugins.Samples;

/// <summary>
/// [샘플 플러그인] 이미지 파일 미리보기 — 비트맵을 로드해 Uniform 맞춤으로 표시.
/// <see cref="PreviewRequest.AvailableWidth"/>가 있으면 그 폭에 맞춰 디코드(<c>DecodePixelWidth</c>)해 메모리 절약 +
/// 영역 리사이즈 시 호스트가 재호출하면 더 선명하게 다시 디코드(크기 상호연동).
/// </summary>
public sealed class ImagePreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff",
    };

    public string Name => "이미지";

    public bool CanPreview(string path) => !Directory.Exists(path) && Exts.Contains(Path.GetExtension(path));

    public async Task<FrameworkElement?> CreatePreviewAsync(PreviewRequest request, CancellationToken ct)
    {
        var file = await StorageFile.GetFileFromPathAsync(request.Path);
        ct.ThrowIfCancellationRequested();
        using var stream = await file.OpenReadAsync();
        ct.ThrowIfCancellationRequested();

        var bmp = new BitmapImage();
        // 영역 크기에 맞춘 디코드(가로 기준, 0=미확정이면 원본). 확대는 하지 않도록 상한만.
        if (request.AvailableWidth >= 1)
        {
            bmp.DecodePixelType = DecodePixelType.Logical;
            bmp.DecodePixelWidth = (int)Math.Min(request.AvailableWidth, 4096);
        }
        await bmp.SetSourceAsync(stream);
        ct.ThrowIfCancellationRequested();

        return new Image
        {
            Source = bmp,
            Stretch = Stretch.Uniform,   // 비율 유지, 영역 안에 맞춤
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
