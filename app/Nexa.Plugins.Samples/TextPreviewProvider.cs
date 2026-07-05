using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexa.Plugins.Preview;

namespace Nexa.Plugins.Samples;

/// <summary>
/// [샘플 플러그인] 텍스트 파일 미리보기 — 앞부분(≤256KB)을 <b>고정폭 TextBlock</b>로 표시하고
/// <b>ScrollViewer로 가로/세로 스크롤</b>한다. TextBlock을 쓰는 이유: WinUI TextBox는 LF(<c>\n</c>)만
/// 있는 텍스트를 <b>한 줄로 렌더</b>하는 문제가 있어(줄바꿈 미인식) 텍스트가 1줄로만 보였다.
/// TextBlock은 <c>\n</c>을 정상적으로 줄바꿈한다.
/// </summary>
public sealed class TextPreviewProvider : IPreviewProvider
{
    private const int MaxBytes = 256 * 1024;

    private static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".json", ".jsonc", ".xml", ".csv", ".tsv", ".ini", ".cfg", ".config",
        ".yml", ".yaml", ".toml", ".env", ".gitignore", ".gitattributes",
        ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".htm", ".css", ".scss",
        ".c", ".h", ".cpp", ".hpp", ".rs", ".go", ".py", ".rb", ".java", ".kt", ".swift", ".php", ".sql",
        ".sh", ".ps1", ".bat", ".cmd", ".props", ".targets", ".csproj", ".sln", ".editorconfig",
    };

    public string Name => "텍스트";

    public bool CanPreview(string path)
    {
        if (Directory.Exists(path))
        {
            return false;
        }
        string ext = Path.GetExtension(path);
        if (ext.Length == 0)
        {
            string name = Path.GetFileName(path);
            return name is "LICENSE" or "README" or "Dockerfile" or "Makefile" or "CHANGELOG";
        }
        return Exts.Contains(ext);
    }

    public async Task<FrameworkElement?> CreatePreviewAsync(PreviewRequest request, CancellationToken ct)
    {
        var (text, truncated) = await Task.Run(() => ReadHead(request.Path), ct);
        ct.ThrowIfCancellationRequested();
        if (truncated)
        {
            text += "\n\n… (이하 생략 — 파일이 256KB보다 큼)";
        }

        // 고정폭 TextBlock(줄바꿈 정상) + 선택 가능. NoWrap → 긴 줄은 가로 스크롤.
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            IsTextSelectionEnabled = true,
            Padding = new Thickness(6, 4, 12, 8),
        };

        // 미리보기 영역 안에서 가로/세로 스크롤. 표시 표준: 완성된 요소(스크롤 포함)를 반환.
        return new ScrollViewer
        {
            Content = block,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
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
        string text = new UTF8Encoding(true, false).GetString(buf, 0, read);
        // 줄바꿈 정규화(CR/CRLF/LF → LF). TextBlock은 LF를 정상 줄바꿈.
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return (text, len > MaxBytes);
    }
}
