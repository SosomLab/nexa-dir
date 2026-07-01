using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace Nexa.App;

/// <summary>파일 항목 종류 (코어 <c>FileKind</c>와 동일 순서: 0=File, 1=Dir, 2=Symlink).</summary>
internal enum NexaFileKind : uint
{
    File = 0,
    Dir = 1,
    Symlink = 2,
}

/// <summary>
/// 디렉터리 엔트리(관리형 DTO). 네이티브 포인터 수명과 무관하게 복사 보관.
/// 인라인 트리(폴더 펼침)를 위해 <see cref="Depth"/>·<see cref="FullPath"/>·<see cref="IsExpanded"/>를 보유한다.
/// </summary>
internal sealed class DirItem : INotifyPropertyChanged
{
    public DirItem(string name, NexaFileKind kind, ulong size, long modifiedUnixMs, string fullPath, int depth)
    {
        Name = name;
        Kind = kind;
        Size = size;
        ModifiedUnixMs = modifiedUnixMs;
        FullPath = fullPath;
        Depth = depth;
    }

    public string Name { get; }
    public NexaFileKind Kind { get; }
    public ulong Size { get; }
    public long ModifiedUnixMs { get; }

    /// <summary>절대 경로(펼침 시 자식 열거용).</summary>
    public string FullPath { get; }

    /// <summary>트리 깊이(0=루트). 이름 셀 들여쓰기에 사용.</summary>
    public int Depth { get; }

    public bool IsDir => Kind == NexaFileKind.Dir;

    /// <summary>표시용 종류 라벨(File/Dir/Symlink).</summary>
    public string KindLabel => Kind.ToString();

    /// <summary>표시용 크기(폴더는 빈 문자열).</summary>
    public string SizeLabel => Kind == NexaFileKind.Dir ? string.Empty : $"{Size:N0} B";

    /// <summary>깊이별 들여쓰기 폭(px). 이름 셀 앞 여백.</summary>
    public double IndentWidth => Depth * 16;

    private bool _isExpanded;

    /// <summary>폴더 펼침 여부. 변경 시 디스클로저 글리프가 갱신된다.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandGlyph)));
            }
        }
    }

    /// <summary>디스클로저 글리프(Segoe MDL2): 폴더=▶(닫힘, ChevronRight)/▼(열림, ChevronDown), 파일=공백.</summary>
    public string ExpandGlyph => !IsDir ? string.Empty : (_isExpanded ? "" : "");

    // ── 표시용 파생 속성(Finder 스타일 목록) ──────────────────────────
    private static readonly SolidColorBrush FolderBrush = new(ColorHelper.FromArgb(0xFF, 0x6C, 0xA8, 0xE0));
    private static readonly SolidColorBrush FileBrush = new(ColorHelper.FromArgb(0xFF, 0xB8, 0xBC, 0xC2));

    /// <summary>종류 아이콘(Segoe MDL2): 폴더/링크/문서.</summary>
    public string IconGlyph => IsDir ? "" : Kind == NexaFileKind.Symlink ? "" : "";

    /// <summary>아이콘 색: 폴더=파랑, 그 외=회색.</summary>
    public Brush IconBrush => IsDir ? FolderBrush : FileBrush;

    /// <summary>폴더명은 굵게.</summary>
    public FontWeight NameWeight => IsDir ? FontWeights.SemiBold : FontWeights.Normal;

    /// <summary>수정한 날짜(로컬, yyyy-MM-dd). 없으면 빈 문자열.</summary>
    public string ModifiedLabel => ModifiedUnixMs < 0
        ? string.Empty
        : DateTimeOffset.FromUnixTimeMilliseconds(ModifiedUnixMs).LocalDateTime.ToString("yyyy-MM-dd");

    /// <summary>종류 텍스트: 폴더/링크/확장자 파일.</summary>
    public string KindText => IsDir
        ? "폴더"
        : Kind == NexaFileKind.Symlink
            ? "링크"
            : Path.GetExtension(Name).TrimStart('.').ToUpperInvariant() is { Length: > 0 } ext ? $"{ext} 파일" : "파일";

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Rust 코어(nexa-interop cdylib)의 C ABI 표면에 대한 P/Invoke 바인딩.
/// dll은 빌드 시 <c>core/target/&lt;profile&gt;/nexa_interop.dll</c> 에서 앱 출력 디렉토리로 복사된다
/// (Nexa.App.csproj의 BuildNexaInterop/CopyNexaInterop 타겟). 상세: docs/18.
/// </summary>
internal static class NativeInterop
{
    private const string Dll = "nexa_interop";

    /// <summary>인터롭 ABI 버전(호환성 점검용). 불일치 시 로드 거부 등에 사용 예정.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint nexa_abi_version();

    /// <summary>왕복 PoC: 두 정수의 합을 코어(Rust)에서 계산해 반환.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nexa_poc_add(int a, int b);

    // ── 디렉터리 스트리밍 열거 (핸들 기반) ──────────────────────────────────

    /// <summary>C ABI <c>NexaEntry</c> 미러. <c>Name</c>은 다음 next/close 전까지만 유효한 네이티브 포인터.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NexaEntry
    {
        public IntPtr Name;
        public uint Kind;
        public ulong Size;
        public long ModifiedUnixMs;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_dir_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_dir_next(IntPtr handle, ref NexaEntry entry);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_dir_close(IntPtr handle);

    /// <summary>
    /// 디렉터리를 열거해 엔트리 목록을 반환한다(open→next 반복→close 래핑).
    /// 네이티브 <c>name</c> 포인터는 즉시 관리형 문자열로 복사해 수명 문제를 차단한다.
    /// 폴더 우선 + 이름 오름차순 정렬. <paramref name="depth"/>는 트리 들여쓰기용.
    /// </summary>
    /// <exception cref="IOException">열기 실패(없는 경로·권한 등).</exception>
    public static IReadOnlyList<DirItem> ReadDir(string path, int depth = 0)
    {
        IntPtr handle = nexa_dir_open(path);
        if (handle == IntPtr.Zero)
        {
            throw new IOException($"디렉터리 열기 실패: {path}");
        }

        var items = new List<DirItem>();
        try
        {
            var entry = default(NexaEntry);
            while (nexa_dir_next(handle, ref entry) == 1)
            {
                string name = Marshal.PtrToStringUTF8(entry.Name) ?? string.Empty;
                var kind = (NexaFileKind)entry.Kind;
                items.Add(new DirItem(name, kind, entry.Size, entry.ModifiedUnixMs, Path.Combine(path, name), depth));
            }
        }
        finally
        {
            nexa_dir_close(handle);
        }

        // 폴더 먼저, 그다음 이름 오름차순(대소문자 무시).
        items.Sort((a, b) => a.IsDir != b.IsDir
            ? (a.IsDir ? -1 : 1)
            : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return items;
    }
}
