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
    public DirItem(string name, NexaFileKind kind, ulong size, long modifiedUnixMs, string fullPath, int depth, uint attrs = 0, ulong id = 0)
    {
        Name = name;
        Kind = kind;
        Size = size;
        ModifiedUnixMs = modifiedUnixMs;
        FullPath = fullPath;
        Depth = depth;
        Attrs = attrs;
        Id = id;
    }

    public string Name { get; }
    public NexaFileKind Kind { get; }
    public ulong Size { get; }
    public long ModifiedUnixMs { get; }

    /// <summary>코어 트리 노드 식별자(NodeId). 코어 트리에서 온 행만 의미(그 외 0). 펼침/선택 코어 호출에 사용.</summary>
    public ulong Id { get; }

    /// <summary>Windows 파일 속성 비트(코어가 열거 시 채움). 비Windows/미조회는 0.</summary>
    public uint Attrs { get; }

    /// <summary>절대 경로(펼침 시 자식 열거용).</summary>
    public string FullPath { get; }

    /// <summary>트리 깊이(0=루트). 이름 셀 들여쓰기에 사용.</summary>
    public int Depth { get; }

    public bool IsDir => Kind == NexaFileKind.Dir;

    /// <summary>표시용 크기(폴더는 빈 문자열) — 탐색기식 적응 단위(KB→MB→GB→TB, 최소 KB).</summary>
    public string SizeLabel => Kind == NexaFileKind.Dir ? string.Empty : FormatSize(Size);

    /// <summary>탐색기식 크기 표기 — 1MB 미만은 올림 KB(컬럼 관례), 이상은 유효 3자리 MB/GB/TB.</summary>
    private static string FormatSize(ulong bytes)
    {
        if (bytes == 0)
        {
            return "0 KB";
        }
        double kb = bytes / 1024.0;
        if (kb < 1000)
        {
            return $"{Math.Ceiling(kb):N0} KB";   // 1B~999KB: 올림 KB(탐색기 Size 컬럼과 동일)
        }
        double mb = kb / 1024.0;
        if (mb < 1000)
        {
            return $"{Sig3(mb)} MB";
        }
        double gb = mb / 1024.0;
        return gb < 1000 ? $"{Sig3(gb)} GB" : $"{Sig3(gb / 1024.0)} TB";
    }

    /// <summary>유효 3자리(1.39 → 13.9 → 139) — StrFormatByteSize 관례.</summary>
    private static string Sig3(double v) => v.ToString(v < 10 ? "0.##" : v < 100 ? "0.#" : "0");

    /// <summary>정보 패널용 원시 바이트 표기 — "13,014 Bytes"(1이면 "1 Byte"). 폴더는 빈 문자열.</summary>
    public string SizeBytesLabel => Kind == NexaFileKind.Dir
        ? string.Empty
        : $"{Size:N0} {(Size == 1 ? "Byte" : "Bytes")}";

    /// <summary>깊이별 들여쓰기 폭(px). 이름 셀 앞 여백.</summary>
    public double IndentWidth => Depth * 16;

    // ── 인라인 이름 변경(선택 후 재클릭 / F2) ─────────────────────────
    private bool _isRenaming;

    /// <summary>편집 모드 여부. 참이면 이름 셀이 <c>TextBox</c>로 전환된다.</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming != value)
            {
                _isRenaming = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRenaming)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditVisibility)));
            }
        }
    }

    private string _editName = string.Empty;

    /// <summary>편집 중 입력값(TextBox 양방향 바인딩). 시작 시 <see cref="Name"/>으로 초기화.</summary>
    public string EditName
    {
        get => _editName;
        set
        {
            if (_editName != value)
            {
                _editName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditName)));
            }
        }
    }

    /// <summary>이름 표시(비편집) 가시성.</summary>
    public Visibility NameVisibility => _isRenaming ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>편집 TextBox 가시성.</summary>
    public Visibility EditVisibility => _isRenaming ? Visibility.Visible : Visibility.Collapsed;

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

    /// <summary>폴더명 굵게(설정 FolderBold, 기본 ON). 변경은 패널 재로드 시 반영(x:Bind OneTime).</summary>
    public FontWeight NameWeight => IsDir && AppSettings.Fonts.FolderBold ? FontWeights.SemiBold : FontWeights.Normal;

    /// <summary>수정 로컬 시각(없으면 null). 날짜/시간 컬럼 라벨의 공통 소스(COL-D1).</summary>
    private DateTime? ModifiedLocal => ModifiedUnixMs < 0
        ? null
        : DateTimeOffset.FromUnixTimeMilliseconds(ModifiedUnixMs).LocalDateTime;

    /// <summary>DateTime modified 컬럼(기본 표시). 형식 yy/MM/dd HH:mm. 없으면 빈 문자열. (차후 YYYY/MM/DD HH:MM:SS 옵션.)</summary>
    public string ModifiedDateTimeLabel => ModifiedLocal?.ToString("yy/MM/dd HH:mm") ?? string.Empty;

    /// <summary>Date modified 컬럼. 형식 yy/MM/dd. 없으면 빈 문자열.</summary>
    public string ModifiedDateLabel => ModifiedLocal?.ToString("yy/MM/dd") ?? string.Empty;

    /// <summary>Time modified 컬럼. 형식 HH:mm. 없으면 빈 문자열.</summary>
    public string ModifiedTimeLabel => ModifiedLocal?.ToString("HH:mm") ?? string.Empty;

    /// <summary>수정한 날짜(로컬, yyyy-MM-dd). 없으면 빈 문자열. (구 기본 · 하위호환.)</summary>
    public string ModifiedLabel => ModifiedLocal?.ToString("yyyy-MM-dd") ?? string.Empty;

    /// <summary>확장자(점 제외, 소문자). 폴더/링크/무확장자는 빈 문자열. 확장자 컬럼용(COL-1).</summary>
    public string Extension => IsDir || Kind == NexaFileKind.Symlink
        ? string.Empty
        : Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();

    /// <summary>종류 텍스트: 폴더/링크/확장자 파일(i18n).</summary>
    public string KindText => IsDir
        ? Loc.T("kind.folder")
        : Kind == NexaFileKind.Symlink
            ? Loc.T("kind.link")
            : Path.GetExtension(Name).TrimStart('.').ToUpperInvariant() is { Length: > 0 } ext ? Loc.T("kind.extFile", ext) : Loc.T("kind.file");

    // ── 선택/호버/포커스 비주얼(Explorer식) ───────────────────────────
    // 선택(활성 패널)=연한 파랑+파란 테두리, 선택(비활성=포커스아웃)=회색, 호버=옅은 파랑.
    private static readonly SolidColorBrush RowTransparent = new(ColorHelper.FromArgb(0x00, 0, 0, 0));
    private static readonly SolidColorBrush SelectedActiveBrush = new(ColorHelper.FromArgb(0x66, 0x66, 0xB3, 0xFF));
    private static readonly SolidColorBrush SelectedInactiveBrush = new(ColorHelper.FromArgb(0x77, 0x70, 0x70, 0x70));
    private static readonly SolidColorBrush HoverBrush = new(ColorHelper.FromArgb(0x33, 0x99, 0xCC, 0xF5));
    private static readonly SolidColorBrush SelBorderActiveBrush = new(ColorHelper.FromArgb(0xFF, 0x3B, 0x82, 0xC4));
    private static readonly SolidColorBrush SelBorderInactiveBrush = new(ColorHelper.FromArgb(0xFF, 0x8A, 0x8A, 0x8A));
    private static readonly SolidColorBrush CaretBorderBrush = new(ColorHelper.FromArgb(0xAA, 0xBB, 0xBB, 0xBB));
    // 4면 1px 박스 유지. 인접 선택 행의 테두리 2px 겹침은 행 템플릿의 상단 -1px 겹침(Margin)으로
    // 해소 — 아래 행의 위 테두리가 위 행의 아래 테두리와 같은 픽셀에 그려져 경계가 1px로 보인다.
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

    /// <summary>선택 시 1px 박스 테두리(4면).</summary>
    public Thickness RowBorderThickness => SelBorderThickness;   // 항상 동일 두께(투명↔색만) → 선택 시 높이 점프 방지

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
    public const uint ExpectedAbi = 7;

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

    /// <summary>코어의 <c>NexaSortKey</c> 실제 크기(바이트). 마샬 레이아웃 동치 점검용.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong nexa_sort_key_size();

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
        CheckLayout("NexaSortKey", Marshal.SizeOf<NexaSortKey>(), nexa_sort_key_size());
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

    // 디렉터리 스트리밍 열거(nexa_dir_*)는 C1에서 코어 트리(nexa_tree_*)로 대체됨 — 관련
    // C# 경로(ReadDir/IsVisible/SortItems + nexa_dir_open/next/close 바인딩)는 미사용이라 제거(E17).
    // 가시성 필터·정렬은 이제 코어(nexa-tree)가 소유(F24·F26). NexaEntry 미러/nexa_entry_size는
    // ABI 레이아웃 가드(VerifyAbi)가 계속 사용하므로 유지.

    // ── 코어 트리/선택 (C1, ABI v4) — 구조체 미러 + 관리형 클라이언트 ─────────────
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

    /// <summary>C ABI <c>NexaSortKey</c> 미러(정렬 서술자). <c>Key</c>: 0=Name 1=Ext 2=Size 3=Modified 4=Kind 5=None, <c>Desc</c>: 0=오름/1=내림.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NexaSortKey
    {
        public uint Key;
        public uint Desc;

        public NexaSortKey(uint key, bool desc)
        {
            Key = key;
            Desc = desc ? 1u : 0u;
        }
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_tree_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte showHidden, byte showDotFiles);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void nexa_tree_close(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong nexa_tree_visible_len(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_row(IntPtr handle, ulong index, ref NexaRow row);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern long nexa_tree_index_of(IntPtr handle, ulong id);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern long nexa_tree_index_of_path(
        IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_expand_path(
        IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ref NexaRange range);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr nexa_tree_row_path(IntPtr handle, ulong index);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_expand(IntPtr handle, ulong id, ref NexaRange range);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_collapse(IntPtr handle, ulong id, ref NexaRange range);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nexa_tree_set_sort(IntPtr handle, NexaSortKey[]? keys, ulong count, int foldersFirst);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern long nexa_tree_find_prefix(
        IntPtr handle, long caret, [MarshalAs(UnmanagedType.LPUTF8Str)] string prefix, uint scope);

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

    /// <summary>트리를 연다(가시성 필터 적용, 실패 시 <see cref="IntPtr.Zero"/>). <see cref="TreeClose"/>로 해제.</summary>
    internal static IntPtr TreeOpen(string path, bool showHidden, bool showDotFiles) =>
        nexa_tree_open(path, (byte)(showHidden ? 1 : 0), (byte)(showDotFiles ? 1 : 0));

    /// <summary>트리 핸들 해제(널 무시).</summary>
    internal static void TreeClose(IntPtr handle) => nexa_tree_close(handle);

    /// <summary>가시 행 수.</summary>
    internal static int TreeVisibleLen(IntPtr handle) => (int)nexa_tree_visible_len(handle);

    /// <summary>노드 <paramref name="id"/>의 가시 인덱스(없으면 -1). 행 재실체화 없이 단일 호출로 조회.</summary>
    internal static int TreeIndexOf(IntPtr handle, ulong id) => (int)nexa_tree_index_of(handle, id);

    /// <summary>경로가 일치하는 가시 행 인덱스(없으면 -1). 끝 구분자·대소문자 무시(코어 매칭, 감사 P3).</summary>
    internal static int TreeIndexOfPath(IntPtr handle, string path) =>
        (int)nexa_tree_index_of_path(handle, path);

    /// <summary>경로로 지정한 가시 폴더를 펼친다(F18 복원). 반환: 변경 구간(무변경이면 전부 0).</summary>
    internal static TreeRange TreeExpandPath(IntPtr handle, string path)
    {
        var rg = default(NexaRange);
        nexa_tree_expand_path(handle, path, ref rg);
        return new TreeRange((int)rg.Start, (int)rg.Removed, (int)rg.Inserted);
    }

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

    /// <summary>정렬 사양 설정 — 로드된 모든 폴더 자식 + 가시목록 재정렬(펼침 보존, COL-2b). <paramref name="keys"/>가 비면 열거 순서.</summary>
    internal static void TreeSetSort(IntPtr handle, NexaSortKey[] keys, bool foldersFirst)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }
        nexa_tree_set_sort(handle, keys, (ulong)keys.Length, foldersFirst ? 1 : 0);
    }

    /// <summary>
    /// 타입어헤드 접두사 매칭(docs/32) — <paramref name="prefix"/>로 시작하는 가시 행 인덱스(없으면 -1).
    /// <paramref name="caret"/>=현재 캐럿 가시 인덱스(-1=없음), <paramref name="scope"/>=0 GlobalFirst/1 CurrentLevel/2 VisibleStream.
    /// </summary>
    internal static int TreeFindPrefix(IntPtr handle, int caret, string prefix, uint scope) =>
        handle == IntPtr.Zero ? -1 : (int)nexa_tree_find_prefix(handle, caret, prefix, scope);

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
