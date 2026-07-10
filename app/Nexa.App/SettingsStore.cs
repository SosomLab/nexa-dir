using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Dispatching;

namespace Nexa.App;

// ─────────────────────────────────────────────────────────────────────────────
// 사용자 설정 영속화(settings.json) — 탭 세션(session.json)과 별도 파일·별도 위치(로밍).
//   · 위치: %APPDATA%\NexaDir\settings.json (여러 PC 로밍 자연스러움 — docs/34).
//   · 대상: 지금까지 인메모리로만 있던 옵션 4벌(Theme/View/Menu/Tab) — 재시작 소실 해소(docs/40 PREF-1).
//   · 저장 규율: SessionStore와 동일(요청/수행 분리·유휴 실행·무변경 스킵·원자적 쓰기·종료 flush).
//     설정은 사용자 편집이라 변경 지점(테마·표시 토글·메뉴·설정 창)에서 MarkDirty + 종료 flush.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>설정 루트(settings.json). 섹션별 스키마(docs/40 §4). 버전으로 후속 마이그레이션 대비.</summary>
internal sealed class SettingsState
{
    public int Version { get; set; } = 1;
    public AppearanceSettings Appearance { get; set; } = new();
    public FontSettings Fonts { get; set; } = new();
    public ToolbarSettings Toolbar { get; set; } = new();
    public TerminalSettings Terminal { get; set; } = new();
    public ViewSettings View { get; set; } = new();
    public SortSettings Sort { get; set; } = new();
    public MenuSettings Menu { get; set; } = new();
    public TabSettings Tab { get; set; } = new();
    public GeneralSettings General { get; set; } = new();
}

/// <summary>글꼴 — 영역별 6종 슬롯(PREF-3). 스키마는 <see cref="FontOptions"/>와 1:1.</summary>
internal sealed class FontSettings
{
    public string BaseFamily { get; set; } = "Segoe UI";
    public double BaseSize { get; set; } = 12;
    public string ConsoleFamily { get; set; } = "Consolas";
    public double ConsoleSize { get; set; } = 13;
    public string StatusFamily { get; set; } = "Segoe UI";
    public double StatusSize { get; set; } = 12;
    public string MenuFamily { get; set; } = "Segoe UI";
    public double MenuSize { get; set; } = 12;
    public string ListFamily { get; set; } = "Segoe UI";
    public double ListSize { get; set; } = 12;
    public bool FolderBold { get; set; } = true;
    public bool HeaderBold { get; set; } = true;
    public bool HeaderItalic { get; set; }
}

/// <summary>정렬 — 폴더 우선 등.</summary>
internal sealed class SortSettings
{
    public bool FoldersFirst { get; set; } = true;
}

/// <summary>도구 모음 — 그룹/항목 표시 순서(docs/44). 스키마는 <see cref="ToolbarOptions"/>와 1:1.</summary>
internal sealed class ToolbarSettings
{
    public List<string> GroupOrder { get; set; } = new();
    public Dictionary<string, List<string>> ItemOrder { get; set; } = new();
}

/// <summary>터미널 — 긴 출력 처리(BP-T). 스키마는 <see cref="TerminalOptions"/>와 1:1.</summary>
internal sealed class TerminalSettings
{
    public bool NoWrap { get; set; } = true;
    public int MaxColumns { get; set; } = 240;
}

/// <summary>일반 — 언어(i18n).</summary>
internal sealed class GeneralSettings
{
    public string Culture { get; set; } = string.Empty;
}

/// <summary>모양 — 테마 모드(기본 System — OS 추종). 후속: 테마팩(docs/39 §5).</summary>
internal sealed class AppearanceSettings
{
    public AppThemeMode Mode { get; set; } = AppThemeMode.System;
}

/// <summary>보기/레이아웃 — 가시성·헤더·전송창·타입어헤드·dwell.</summary>
internal sealed class ViewSettings
{
    public bool ShowHiddenFiles { get; set; } = true;
    public bool ShowDotFiles { get; set; } = true;
    public bool ShowPathHeader { get; set; }
    public bool AutoCloseTransferWindow { get; set; } = true;
    public bool UseSystemClipboard { get; set; }
    public double UpNavTargetAlign { get; set; } = 0.5;
    public int TabDwellMs { get; set; } = 2000;
    public int FolderDwellMs { get; set; } = 3000;
    public uint TypeAheadScope { get; set; } = 2;
    public long TypeAheadTimeoutMs { get; set; } = 1000;
    public HudPosition TypeAheadHudPosition { get; set; } = HudPosition.BottomLeft;
    public bool TypeAheadSpecialChars { get; set; } = true;
    public bool TypeAheadSpace { get; set; } = true;
    public bool TypeAheadBackspace { get; set; } = true;
}

/// <summary>컨텍스트 메뉴 커스텀 항목(docs/38 §7).</summary>
internal sealed class MenuSettings
{
    public List<string> DisabledItems { get; set; } = new();
    public Dictionary<string, int> OrderOverrides { get; set; } = new();
    public bool CustomSectionOnTop { get; set; }
}

/// <summary>탭 옵션.</summary>
internal sealed class TabSettings
{
    public TabDoubleClickAction DoubleClick { get; set; } = TabDoubleClickAction.Close;
}

/// <summary>
/// <see cref="SettingsState"/> 저장/로드 엔진 — <see cref="SessionStore"/>와 동일 규율(디바운스·유휴·무변경 스킵·
/// 원자적 쓰기·종료 flush). UI 스레드(DispatcherQueue)에서 생성·구동.
/// </summary>
internal sealed class SettingsStore
{
    private readonly string _path;
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<SettingsState> _capture;
    private readonly DispatcherQueueTimer _tick;

    private volatile bool _dirty;
    private string _lastHash = string.Empty;
    private bool _flushed;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },   // 열거는 문자열로(가독·안정)
    };

    public SettingsStore(string path, DispatcherQueue dispatcher, Func<SettingsState> capture)
    {
        _path = path;
        _dispatcher = dispatcher;
        _capture = capture;
        _tick = dispatcher.CreateTimer();
        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.IsRepeating = true;
        _tick.Tick += (_, _) => OnTick();
        _tick.Start();
    }

    /// <summary>저장 요청(초저비용·멱등) — 다음 Tick에서 1회만 저장.</summary>
    public void MarkDirty() => _dirty = true;

    private void OnTick()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, FlushIfChanged);
    }

    private void FlushIfChanged()
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(_capture(), JsonOpts);
        }
        catch
        {
            return;
        }
        string hash = Hash(json);
        if (hash == _lastHash)
        {
            return;
        }
        if (WriteAtomic(json))
        {
            _lastHash = hash;
        }
    }

    /// <summary>종료 시 즉시(동기) 저장(창 Closed·ProcessExit에서 1회).</summary>
    public void Flush()
    {
        if (_flushed)
        {
            return;
        }
        _flushed = true;
        _dirty = false;
        _tick.Stop();
        FlushIfChanged();
    }

    private bool WriteAtomic(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            if (File.Exists(_path))
            {
                File.Replace(tmp, _path, null);
            }
            else
            {
                File.Move(tmp, _path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>설정 파일 로드(없거나 손상 시 null → 기본값). 예외 격리.</summary>
    public static SettingsState? Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SettingsState>(File.ReadAllText(path), JsonOpts)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>로드한 상태를 <see cref="AppSettings"/> 인메모리 그룹에 적용(시작 시 1회).</summary>
    public static void Apply(SettingsState? s)
    {
        if (s is null)
        {
            return;
        }
        AppSettings.Theme.Mode = s.Appearance.Mode;

        var f = AppSettings.Fonts;
        f.BaseFamily = s.Fonts.BaseFamily;
        f.BaseSize = s.Fonts.BaseSize;
        f.ConsoleFamily = s.Fonts.ConsoleFamily;
        f.ConsoleSize = s.Fonts.ConsoleSize;
        f.StatusFamily = s.Fonts.StatusFamily;
        f.StatusSize = s.Fonts.StatusSize;
        f.MenuFamily = s.Fonts.MenuFamily;
        f.MenuSize = s.Fonts.MenuSize;
        f.ListFamily = s.Fonts.ListFamily;
        f.ListSize = s.Fonts.ListSize;
        f.FolderBold = s.Fonts.FolderBold;
        f.HeaderBold = s.Fonts.HeaderBold;
        f.HeaderItalic = s.Fonts.HeaderItalic;

        AppSettings.Sort.FoldersFirst = s.Sort.FoldersFirst;

        var tb = AppSettings.Toolbar;
        tb.GroupOrder.Clear();
        tb.GroupOrder.AddRange(s.Toolbar.GroupOrder);
        tb.ItemOrder.Clear();
        foreach (var kv in s.Toolbar.ItemOrder) { tb.ItemOrder[kv.Key] = new List<string>(kv.Value); }

        AppSettings.Terminal.NoWrap = s.Terminal.NoWrap;
        AppSettings.Terminal.MaxColumns = s.Terminal.MaxColumns;

        var v = AppSettings.View;
        v.ShowHiddenFiles = s.View.ShowHiddenFiles;
        v.ShowDotFiles = s.View.ShowDotFiles;
        v.ShowPathHeader = s.View.ShowPathHeader;
        v.AutoCloseTransferWindow = s.View.AutoCloseTransferWindow;
        v.UseSystemClipboard = s.View.UseSystemClipboard;
        v.UpNavTargetAlign = s.View.UpNavTargetAlign;
        v.TabDwellMs = s.View.TabDwellMs;
        v.FolderDwellMs = s.View.FolderDwellMs;
        v.TypeAheadScope = s.View.TypeAheadScope;
        v.TypeAheadTimeoutMs = s.View.TypeAheadTimeoutMs;
        v.TypeAheadHudPosition = s.View.TypeAheadHudPosition;
        v.TypeAheadSpecialChars = s.View.TypeAheadSpecialChars;
        v.TypeAheadSpace = s.View.TypeAheadSpace;
        v.TypeAheadBackspace = s.View.TypeAheadBackspace;

        var m = AppSettings.Menu;
        m.DisabledItems.Clear();
        foreach (string id in s.Menu.DisabledItems) { m.DisabledItems.Add(id); }
        m.OrderOverrides.Clear();
        foreach (var kv in s.Menu.OrderOverrides) { m.OrderOverrides[kv.Key] = kv.Value; }
        m.CustomSectionOnTop = s.Menu.CustomSectionOnTop;

        AppSettings.Tab.DoubleClick = s.Tab.DoubleClick;
        AppSettings.General.Culture = s.General.Culture;
    }

    /// <summary>현재 <see cref="AppSettings"/>를 직렬화용 상태로 캡처.</summary>
    public static SettingsState Capture()
    {
        var v = AppSettings.View;
        var m = AppSettings.Menu;
        var f = AppSettings.Fonts;
        return new SettingsState
        {
            Appearance = new AppearanceSettings { Mode = AppSettings.Theme.Mode },
            Fonts = new FontSettings
            {
                BaseFamily = f.BaseFamily,
                BaseSize = f.BaseSize,
                ConsoleFamily = f.ConsoleFamily,
                ConsoleSize = f.ConsoleSize,
                StatusFamily = f.StatusFamily,
                StatusSize = f.StatusSize,
                MenuFamily = f.MenuFamily,
                MenuSize = f.MenuSize,
                ListFamily = f.ListFamily,
                ListSize = f.ListSize,
                FolderBold = f.FolderBold,
                HeaderBold = f.HeaderBold,
                HeaderItalic = f.HeaderItalic,
            },
            Sort = new SortSettings { FoldersFirst = AppSettings.Sort.FoldersFirst },
            Toolbar = new ToolbarSettings
            {
                GroupOrder = new List<string>(AppSettings.Toolbar.GroupOrder),
                ItemOrder = AppSettings.Toolbar.ItemOrder.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value)),
            },
            Terminal = new TerminalSettings
            {
                NoWrap = AppSettings.Terminal.NoWrap,
                MaxColumns = AppSettings.Terminal.MaxColumns,
            },
            View = new ViewSettings
            {
                ShowHiddenFiles = v.ShowHiddenFiles,
                ShowDotFiles = v.ShowDotFiles,
                ShowPathHeader = v.ShowPathHeader,
                AutoCloseTransferWindow = v.AutoCloseTransferWindow,
                UseSystemClipboard = v.UseSystemClipboard,
                UpNavTargetAlign = v.UpNavTargetAlign,
                TabDwellMs = v.TabDwellMs,
                FolderDwellMs = v.FolderDwellMs,
                TypeAheadScope = v.TypeAheadScope,
                TypeAheadTimeoutMs = v.TypeAheadTimeoutMs,
                TypeAheadHudPosition = v.TypeAheadHudPosition,
                TypeAheadSpecialChars = v.TypeAheadSpecialChars,
                TypeAheadSpace = v.TypeAheadSpace,
                TypeAheadBackspace = v.TypeAheadBackspace,
            },
            Menu = new MenuSettings
            {
                DisabledItems = new List<string>(m.DisabledItems),
                OrderOverrides = new Dictionary<string, int>(m.OrderOverrides),
                CustomSectionOnTop = m.CustomSectionOnTop,
            },
            Tab = new TabSettings { DoubleClick = AppSettings.Tab.DoubleClick },
            General = new GeneralSettings { Culture = AppSettings.General.Culture },
        };
    }

    /// <summary>설정 파일 표준 경로: <c>%APPDATA%\NexaDir\settings.json</c>(로밍 — 세션과 별도).</summary>
    public static string DefaultPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "NexaDir", "settings.json");
    }
}
