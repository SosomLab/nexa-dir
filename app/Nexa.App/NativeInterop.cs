using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
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
    public DirItem(string name, NexaFileKind kind, ulong size, long modifiedUnixMs, string fullPath, int depth, uint attrs = 0)
    {
        Name = name;
        Kind = kind;
        Size = size;
        ModifiedUnixMs = modifiedUnixMs;
        FullPath = fullPath;
        Depth = depth;
        Attrs = attrs;
    }

    public string Name { get; }
    public NexaFileKind Kind { get; }
    public ulong Size { get; }
    public long ModifiedUnixMs { get; }

    /// <summary>Windows 파일 속성 비트(코어가 열거 시 채움). 비Windows/미조회는 0.</summary>
    public uint Attrs { get; }

    /// <summary>Windows 숨김 속성(FILE_ATTRIBUTE_HIDDEN=0x2) 보유 여부.</summary>
    public bool IsHidden => (Attrs & 0x2) != 0;

    /// <summary>이름이 '.'으로 시작하는 리눅스식 점 파일/폴더인지.</summary>
    public bool IsDotFile => Name.StartsWith('.');

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

    // ── 선택/호버/포커스 비주얼(Explorer식) ───────────────────────────
    // 선택(활성 패널)=연한 파랑+파란 테두리, 선택(비활성=포커스아웃)=회색, 호버=옅은 파랑.
    private static readonly SolidColorBrush RowTransparent = new(ColorHelper.FromArgb(0x00, 0, 0, 0));
    private static readonly SolidColorBrush SelectedActiveBrush = new(ColorHelper.FromArgb(0x66, 0x66, 0xB3, 0xFF));
    private static readonly SolidColorBrush SelectedInactiveBrush = new(ColorHelper.FromArgb(0x77, 0x70, 0x70, 0x70));
    private static readonly SolidColorBrush HoverBrush = new(ColorHelper.FromArgb(0x33, 0x99, 0xCC, 0xF5));
    private static readonly SolidColorBrush SelBorderActiveBrush = new(ColorHelper.FromArgb(0xFF, 0x3B, 0x82, 0xC4));
    private static readonly SolidColorBrush SelBorderInactiveBrush = new(ColorHelper.FromArgb(0xFF, 0x8A, 0x8A, 0x8A));
    private static readonly SolidColorBrush CaretBorderBrush = new(ColorHelper.FromArgb(0xAA, 0xBB, 0xBB, 0xBB));
    private static readonly Thickness SelBorderThickness = new(1);

    private bool _isSelected;
    private bool _isHovered;
    private bool _isCaret;
    private bool _panelFocused = true;

    /// <summary>선택 여부(단일/다중/범위).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; RaiseVisual(); } }
    }

    /// <summary>마우스 호버 여부(선택 아닐 때만 하이라이트).</summary>
    public bool IsHovered
    {
        get => _isHovered;
        set { if (_isHovered != value) { _isHovered = value; RaiseVisual(); } }
    }

    /// <summary>키보드 캐럿(현재 위치) 여부 — 선택되지 않아도 얇은 포커스 외곽선 표시(Ctrl 이동 시 위치 식별).</summary>
    public bool IsCaret
    {
        get => _isCaret;
        set { if (_isCaret != value) { _isCaret = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBorderBrush))); } }
    }

    /// <summary>속한 패널이 활성(포커스)인지 — 비활성이면 선택색이 회색(포커스아웃).</summary>
    public bool PanelFocused
    {
        get => _panelFocused;
        set { if (_panelFocused != value) { _panelFocused = value; RaiseVisual(); } }
    }

    private void RaiseVisual()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBorderBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBorderThickness)));
    }

    /// <summary>행 배경: 선택(활성=파랑/비활성=회색) · 호버(옅은 파랑) · 그 외 투명(전폭 클릭 유지).</summary>
    public Brush RowBackground =>
        _isSelected ? (_panelFocused ? SelectedActiveBrush : SelectedInactiveBrush)
        : _isHovered ? HoverBrush : RowTransparent;

    /// <summary>테두리: 선택(활성=파랑/비활성=회색) &gt; 캐럿(포커스 외곽선) &gt; 없음.</summary>
    public Brush RowBorderBrush =>
        _isSelected ? (_panelFocused ? SelBorderActiveBrush : SelBorderInactiveBrush)
        : _isCaret ? CaretBorderBrush : RowTransparent;

    /// <summary>선택 시 1px 테두리.</summary>
    public Thickness RowBorderThickness => SelBorderThickness;   // 항상 1px(투명↔색만) → 선택 시 높이 점프 방지

    // ── 실제 셸 아이콘(폴더 커스텀/파일 형식) ────────────────────────
    private ImageSource? _iconImage;

    /// <summary>셸 썸네일 아이콘(비동기 로드). null이면 글리프 폴백.</summary>
    public ImageSource? IconImage
    {
        get => _iconImage;
        set
        {
            if (!ReferenceEquals(_iconImage, value))
            {
                _iconImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconImage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconImageVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphVisibility)));
            }
        }
    }

    /// <summary>실제 아이콘이 있으면 이미지 표시.</summary>
    public Visibility IconImageVisibility => _iconImage is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>실제 아이콘이 없으면 글리프 폴백 표시.</summary>
    public Visibility GlyphVisibility => _iconImage is null ? Visibility.Visible : Visibility.Collapsed;

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

    /// <summary>이 앱이 기대하는 코어 ABI 버전. <see cref="VerifyAbi"/>가 로드된 dll과 대조한다.</summary>
    public const uint ExpectedAbi = 3;

    /// <summary>인터롭 ABI 버전(호환성 점검용). <see cref="VerifyAbi"/>가 <see cref="ExpectedAbi"/>와 대조.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint nexa_abi_version();

    /// <summary>코어의 <c>NexaEntry</c> 실제 크기(바이트). 마샬 레이아웃 동치 점검용.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong nexa_entry_size();

    /// <summary>코어의 <c>NexaRow</c> 실제 크기(바이트). 마샬 레이아웃 동치 점검용.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong nexa_row_size();

    /// <summary>코어의 <c>NexaRange</c> 실제 크기(바이트). 마샬 레이아웃 동치 점검용.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong nexa_range_size();

    /// <summary>
    /// 로드된 네이티브 dll의 <b>ABI 호환성</b>을 검사한다(감사 A2/A3 정정):
    /// ① 버전이 <see cref="ExpectedAbi"/>와 일치, ② 마샬 구조체(<c>NexaEntry</c>/<c>NexaRow</c>/<c>NexaRange</c>)
    /// 크기가 코어와 일치. 불일치 시 <see cref="InvalidOperationException"/> — 호출측이 네이티브 경로를 비활성/경고한다.
    /// 구형/신형 dll이 조용히 로드돼 구조체가 오정렬되는 것을 차단한다.
    /// </summary>
    public static void VerifyAbi()
    {
        uint abi = nexa_abi_version();
        if (abi != ExpectedAbi)
        {
            throw new InvalidOperationException(
                $"코어 ABI 불일치: dll={abi}, 기대={ExpectedAbi} — nexa_interop.dll이 구형/신형입니다(재빌드 필요).");
        }
        CheckLayout("NexaEntry", Marshal.SizeOf<NexaEntry>(), nexa_entry_size());
        CheckLayout("NexaRow", Marshal.SizeOf<NexaRow>(), nexa_row_size());
        CheckLayout("NexaRange", Marshal.SizeOf<NexaRange>(), nexa_range_size());
    }

    /// <summary>C# 마샬 크기와 코어 크기를 대조(불일치 시 예외). <see cref="VerifyAbi"/> 내부용.</summary>
    private static void CheckLayout(string name, int marshalSize, ulong nativeSize)
    {
        if ((ulong)marshalSize != nativeSize)
        {
            throw new InvalidOperationException(
                $"{name} 레이아웃 불일치: C#={marshalSize}B, 코어={nativeSize}B — P/Invoke 미러가 어긋났습니다.");
        }
    }

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
        public uint Attrs;   // ABI v2: Windows 파일 속성 비트
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
    /// 정렬은 <paramref name="sort"/>(생략 시 <see cref="AppSettings.Sort"/>)를 따른다 —
    /// 폴더 우선(기본) + 이름 오름차순. 가시성 필터는 <paramref name="view"/>(생략 시
    /// <see cref="AppSettings.View"/>) — 숨김 속성·점(.) 파일을 독립 토글로 걸러낸다.
    /// <paramref name="depth"/>는 트리 들여쓰기용.
    /// </summary>
    /// <exception cref="IOException">열기 실패(없는 경로·권한 등).</exception>
    public static IReadOnlyList<DirItem> ReadDir(string path, int depth = 0, SortOptions? sort = null, ViewOptions? view = null)
    {
        IntPtr handle = nexa_dir_open(path);
        if (handle == IntPtr.Zero)
        {
            throw new IOException($"디렉터리 열기 실패: {path}");
        }

        var v = view ?? AppSettings.View;
        var items = new List<DirItem>();
        try
        {
            var entry = default(NexaEntry);
            while (nexa_dir_next(handle, ref entry) == 1)
            {
                string name = Marshal.PtrToStringUTF8(entry.Name) ?? string.Empty;
                var kind = (NexaFileKind)entry.Kind;
                var it = new DirItem(name, kind, entry.Size, entry.ModifiedUnixMs, Path.Combine(path, name), depth, entry.Attrs);
                if (IsVisible(it, v))
                {
                    items.Add(it);
                }
            }
        }
        finally
        {
            nexa_dir_close(handle);
        }

        SortItems(items, sort ?? AppSettings.Sort);
        return items;
    }

    /// <summary>
    /// 가시성 필터: 숨김 속성 표시 토글과 점(.) 파일 표시 토글을 독립으로 적용한다.
    /// 둘 다 "보기" 개념 — 해제(<c>false</c>)된 종류만 걸러낸다(기본은 둘 다 표시).
    /// </summary>
    internal static bool IsVisible(DirItem it, ViewOptions view)
    {
        if (!view.ShowDotFiles && it.IsDotFile)
        {
            return false;
        }
        if (!view.ShowHiddenFiles && it.IsHidden)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 파일 목록을 제자리 정렬한다. <see cref="SortOptions.FoldersFirst"/>가 켜져 있으면
    /// 폴더를 파일보다 먼저 두고, 같은 그룹 안에서는 이름 오름차순(대소문자 무시).
    /// 정렬 키·방향(A5)은 후속으로 <see cref="SortOptions"/>에 추가한다.
    /// </summary>
    internal static void SortItems(List<DirItem> items, SortOptions sort)
    {
        items.Sort((a, b) =>
            sort.FoldersFirst && a.IsDir != b.IsDir
                ? (a.IsDir ? -1 : 1)
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    // ── 코어 트리/선택 (C1, ABI v3) — 구조체 미러 + 관리형 클라이언트 ─────────────
    // UI 소비(가상화 컬렉션)는 후속. 여기서는 P/Invoke + 마샬 은닉 관리형 API를 제공.

    /// <summary>C ABI <c>NexaRow</c> 미러(코어 <c>VisibleRow</c>). 8→4→1바이트 배치와 일치.
    /// <c>Name</c>은 다음 row/close 전까지만 유효한 네이티브 포인터.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NexaRow
    {
        public ulong Id;
        public ulong Size;
        public long ModifiedUnixMs;
        public IntPtr Name;
        public uint Depth;
        public uint Kind;
        public uint Attrs;
        public byte Expanded;     // 0/1
        public byte HasChildren;  // 0/1
    }

    /// <summary>C ABI <c>NexaRange</c> 미러(코어 <c>RangeChange</c>) — 펼침/접힘 diff 구간.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NexaRange
    {
        public ulong Start;
        public ulong Removed;
        public ulong Inserted;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_tree_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_tree_close(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong nexa_tree_visible_len(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_row(IntPtr handle, ulong index, ref NexaRow row);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_tree_row_path(IntPtr handle, ulong index);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_expand(IntPtr handle, ulong id, ref NexaRange range);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_collapse(IntPtr handle, ulong id, ref NexaRange range);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_tree_select(IntPtr handle, ulong id, uint mode);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_tree_select_range(IntPtr handle, ulong id);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_tree_select_all(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_tree_clear_selection(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_is_selected(IntPtr handle, ulong id);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong nexa_tree_selected_len(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_tree_selected_path(IntPtr handle, ulong index);

    // ── 마샬 은닉 관리형 API (호출측은 TreeRow/TreeRange만 다룸) ─────────────

    /// <summary>트리를 연다(실패 시 <see cref="IntPtr.Zero"/>). <see cref="TreeClose"/>로 해제.</summary>
    internal static IntPtr TreeOpen(string path) => nexa_tree_open(path);

    /// <summary>트리 핸들 해제(널 무시).</summary>
    internal static void TreeClose(IntPtr handle) => nexa_tree_close(handle);

    /// <summary>가시 행 수.</summary>
    internal static int TreeVisibleLen(IntPtr handle) => (int)nexa_tree_visible_len(handle);

    /// <summary>가시 인덱스의 행(범위 밖이면 <c>false</c>). <c>Name</c>은 즉시 관리형 문자열로 복사.</summary>
    internal static bool TreeGetRow(IntPtr handle, int index, out TreeRow row)
    {
        var r = default(NexaRow);
        if (nexa_tree_row(handle, (ulong)index, ref r) != 1)
        {
            row = default;
            return false;
        }
        row = new TreeRow(
            r.Id, r.Depth, (NexaFileKind)r.Kind, Marshal.PtrToStringUTF8(r.Name) ?? string.Empty,
            r.Size, r.ModifiedUnixMs, r.Attrs, r.Expanded != 0, r.HasChildren != 0);
        return true;
    }

    /// <summary>가시 인덱스 행의 절대 경로(범위 밖이면 null). 아이콘/네비게이션용.</summary>
    internal static string? TreeRowPath(IntPtr handle, int index)
    {
        IntPtr p = nexa_tree_row_path(handle, (ulong)index);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }

    /// <summary>펼침 → 가시 목록 변경 구간(IO 오류/무변경은 빈 구간).</summary>
    internal static TreeRange TreeExpand(IntPtr handle, ulong id)
    {
        var rg = default(NexaRange);
        nexa_tree_expand(handle, id, ref rg);
        return new TreeRange((int)rg.Start, (int)rg.Removed, (int)rg.Inserted);
    }

    /// <summary>접힘 → 가시 목록 변경 구간.</summary>
    internal static TreeRange TreeCollapse(IntPtr handle, ulong id)
    {
        var rg = default(NexaRange);
        nexa_tree_collapse(handle, id, ref rg);
        return new TreeRange((int)rg.Start, (int)rg.Removed, (int)rg.Inserted);
    }

    /// <summary>선택: <paramref name="mode"/> 0=단일, 1=토글.</summary>
    internal static void TreeSelect(IntPtr handle, ulong id, uint mode) => nexa_tree_select(handle, id, mode);

    /// <summary>anchor~id 가시 범위 선택.</summary>
    internal static void TreeSelectRange(IntPtr handle, ulong id) => nexa_tree_select_range(handle, id);

    /// <summary>가시 노드 전체 선택.</summary>
    internal static void TreeSelectAll(IntPtr handle) => nexa_tree_select_all(handle);

    /// <summary>선택 해제.</summary>
    internal static void TreeClearSelection(IntPtr handle) => nexa_tree_clear_selection(handle);

    /// <summary>id 선택 여부.</summary>
    internal static bool TreeIsSelected(IntPtr handle, ulong id) => nexa_tree_is_selected(handle, id) != 0;

    /// <summary>선택 수.</summary>
    internal static int TreeSelectedLen(IntPtr handle) => (int)nexa_tree_selected_len(handle);

    /// <summary>선택(삽입 순서) index번째 경로(없으면 null).</summary>
    internal static string? TreeSelectedPath(IntPtr handle, int index)
    {
        IntPtr p = nexa_tree_selected_path(handle, (ulong)index);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }
}

/// <summary>코어 트리 가시 행(관리형). 코어 <c>VisibleRow</c>/C ABI <c>NexaRow</c>의 관리형 표현.</summary>
internal readonly record struct TreeRow(
    ulong Id,
    uint Depth,
    NexaFileKind Kind,
    string Name,
    ulong Size,
    long ModifiedUnixMs,
    uint Attrs,
    bool Expanded,
    bool HasChildren);

/// <summary>펼침/접힘으로 인한 가시 목록 변경 구간(관리형).</summary>
internal readonly record struct TreeRange(int Start, int Removed, int Inserted);
