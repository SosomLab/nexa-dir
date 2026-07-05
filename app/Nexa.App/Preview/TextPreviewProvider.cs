using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nexa.App.Preview;

/// <summary>텍스트 파일 미리보기(BP-2) — 앞부분(≤256KB)을 읽어 고정폭 읽기전용 TextBox로 표시. 큰 파일은 잘림 표시.</summary>
public sealed class TextPreviewProvider : IPreviewProvider
{
    private const int MaxBytes = 256 * 1024;

    private static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".json", ".jsonc", ".xml", ".csv", ".tsv", ".ini", ".cfg", ".config",
        ".yml", ".yaml", ".toml", ".env", ".gitignore", ".gitattributes",
        ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".htm", ".css", ".scss",
        ".c", ".h", ".cpp", ".hpp", ".rs", ".go", ".py", ".rb", ".java", ".kt", ".swift", ".php", ".sql",
        ".sh", ".ps1", ".bat", ".cmd", ".ps1xml", ".props", ".targets", ".csproj", ".sln", ".editorconfig",
    };

    public string Name => "텍스트";

    public bool CanPreview(string path)
    {
        if (Directory.Exists(path))
        {
            return false;
        }
        string ext = Path.GetExtension(path);
        // 확장자 없는 흔한 텍스트(예: LICENSE, README, Dockerfile)도 허용.
        if (ext.Length == 0)
        {
            string name = Path.GetFileName(path);
            return name is "LICENSE" or "README" or "Dockerfile" or "Makefile" or "CHANGELOG";
        }
        return Exts.Contains(ext);
    }

    public async Task<FrameworkElement?> CreatePreviewAsync(string path, CancellationToken ct)
    {
        var (text, truncated) = await Task.Run(() => ReadHead(path), ct);
        ct.ThrowIfCancellationRequested();
        if (truncated)
        {
            text += "\n\n… (이하 생략 — 파일이 256KB보다 큼)";
        }
        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            IsSpellCheckEnabled = false,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        return box;
    }

    private static (string text, bool truncated) ReadHead(string path)
    {
        using var fs = File.OpenRead(path);
        long len = fs.Length;
        int toRead = (int)Math.Min(MaxBytes, len);
        var buf = new byte[toRead];
        int read = 0;
        while (read < toRead)
        {
            int n = fs.Read(buf, read, toRead - read);
            if (n <= 0)
            {
                break;
            }
            read += n;
        }
        // UTF-8(BOM 허용, 유효하지 않은 바이트는 대체문자). 후속: 인코딩 감지.
        string text = new UTF8Encoding(true, false).GetString(buf, 0, read);
        return (text, len > MaxBytes);
    }
}
