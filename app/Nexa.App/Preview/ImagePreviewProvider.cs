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

namespace Nexa.App.Preview;

/// <summary>이미지 파일 미리보기(BP-2) — 비트맵을 로드해 Uniform 맞춤으로 표시.</summary>
public sealed class ImagePreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff",
    };

    public string Name => "이미지";

    public bool CanPreview(string path) => !Directory.Exists(path) && Exts.Contains(Path.GetExtension(path));

    public async Task<FrameworkElement?> CreatePreviewAsync(string path, CancellationToken ct)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        ct.ThrowIfCancellationRequested();
        using var stream = await file.OpenReadAsync();
        ct.ThrowIfCancellationRequested();

        var bmp = new BitmapImage();
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
