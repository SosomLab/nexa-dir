using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Nexa.App;

/// <summary>
/// 통합 설정 창(docs/40 PREF-1) — <b>VS Code식</b>: 상단 설정 검색 + 좌측 카테고리 트리 + 우측 편집기.
/// 모든 영속 설정(settings.json 대상)을 <b>설정 레지스트리</b>(<see cref="Entry"/> 목록)로 등록해
/// 카테고리 렌더와 검색이 같은 원천을 쓴다. 각 컨트롤은 <see cref="AppSettings"/>를 직접 편집하고,
/// 변경 시 호스트(<see cref="MainWindow.OnPreferencesChanged"/>)가 라이브 적용 + 저장.
/// 문자열은 <see cref="Loc"/>(i18n). 후속: 컬럼·단축키·런처·즐겨찾기 카테고리(docs/40 §3).
/// </summary>
public sealed partial class PreferencesWindow : Window
{
    private readonly MainWindow _host;
    private bool _building;   // 초기 구성 중 이벤트 발화로 인한 저장 소음 방지

    /// <summary>카테고리(트리 노드). 1단계 중첩만 사용(Parent=최상위 키). NoteKey=카테고리 하단 안내문.</summary>
    private sealed record Category(string Key, string LabelKey, string? Parent = null, string? NoteKey = null);

    /// <summary>설정 항목 1개 — 이름/설명/설정값이 한 논리 그룹(검색의 최소 단위). Build()가 라벨·설명
    /// 포함 편집 컨트롤을 만들고, 검색은 <b>제목(LabelKey)·설명(DescKey)</b> 일치만 본다.</summary>
    private sealed record Entry(string CategoryKey, string LabelKey, Func<FrameworkElement> Build, string? DescKey = null);

    private readonly List<Category> _categories;
    private readonly List<Entry> _entries;
    private readonly Dictionary<TreeViewNode, string> _nodeKeys = new();
    private string _currentCategory = "appearance";
    private string _query = string.Empty;   // 현재 검색어(빈 문자열=검색 없음) — 트리·우측 렌더 공용

    internal PreferencesWindow(MainWindow host)
    {
        InitializeComponent();
        _host = host;
        Title = Loc.T("pref.title");
        AppWindow.Resize(new SizeInt32(880, 640));
        SearchBox.PlaceholderText = Loc.T("pref.search.placeholder");

        _categories = new List<Category>
        {
            new("appearance", "pref.page.appearance"),
            new("theme", "pref.appearance.theme", Parent: "appearance", NoteKey: "pref.appearance.note"),
            new("fonts", "pref.fonts.title", Parent: "appearance", NoteKey: "pref.fonts.note"),
            new("list", "pref.cat.list"),
            new("tabs", "pref.cat.tabs"),
            new("toolbar", "pref.toolbar.title", NoteKey: "pref.toolbar.note"),
            new("terminal", "pref.cat.terminal"),
            new("ops", "pref.cat.ops"),
            new("menu", "pref.menu.title", NoteKey: "pref.menu.note"),
            new("lang", "pref.lang.title", NoteKey: "pref.lang.restartNote"),
        };
        _entries = BuildRegistry();
        BuildTree();
        ShowCategory("appearance");
    }

    private void Changed()
    {
        if (!_building)
        {
            _host.OnPreferencesChanged();
        }
    }

    // ── 설정 레지스트리 — 영속 대상 전부(테마·글꼴 6종·목록·탭·파일 작업·메뉴·언어) ─────
    private List<Entry> BuildRegistry() => new()
    {
        // 모양 › 테마
        new("theme", "pref.appearance.theme", ThemeRadios),
        // 모양 › 글꼴(PREF-3) — 6종 슬롯
        new("fonts", "pref.fonts.base", () => FontRow("pref.fonts.base", "pref.fonts.baseDesc",
            () => AppSettings.Fonts.BaseFamily, v => AppSettings.Fonts.BaseFamily = v,
            () => AppSettings.Fonts.BaseSize, v => AppSettings.Fonts.BaseSize = v), DescKey: "pref.fonts.baseDesc"),
        new("fonts", "pref.fonts.console", () => FontRow("pref.fonts.console", "pref.fonts.consoleDesc",
            () => AppSettings.Fonts.ConsoleFamily, v => AppSettings.Fonts.ConsoleFamily = v,
            () => AppSettings.Fonts.ConsoleSize, v => AppSettings.Fonts.ConsoleSize = v), DescKey: "pref.fonts.consoleDesc"),
        new("fonts", "pref.fonts.menu", () => FontRow("pref.fonts.menu", "pref.fonts.menuDesc",
            () => AppSettings.Fonts.MenuFamily, v => AppSettings.Fonts.MenuFamily = v,
            () => AppSettings.Fonts.MenuSize, v => AppSettings.Fonts.MenuSize = v), DescKey: "pref.fonts.menuDesc"),
        new("fonts", "pref.fonts.status", () => FontRow("pref.fonts.status", "pref.fonts.statusDesc",
            () => AppSettings.Fonts.StatusFamily, v => AppSettings.Fonts.StatusFamily = v,
            () => AppSettings.Fonts.StatusSize, v => AppSettings.Fonts.StatusSize = v), DescKey: "pref.fonts.statusDesc"),
        new("fonts", "pref.fonts.list", () => FontRow("pref.fonts.list", "pref.fonts.listDesc",
            () => AppSettings.Fonts.ListFamily, v => AppSettings.Fonts.ListFamily = v,
            () => AppSettings.Fonts.ListSize, v => AppSettings.Fonts.ListSize = v), DescKey: "pref.fonts.listDesc"),
        new("fonts", "pref.fonts.folderBold", () => CheckRow("pref.fonts.folderBold",
            AppSettings.Fonts.FolderBold, v => AppSettings.Fonts.FolderBold = v)),
        new("fonts", "pref.fonts.header", HeaderFontRow, DescKey: "pref.fonts.headerDesc"),
        // 파일 목록
        new("list", "pref.layout.hidden", () => CheckRow("pref.layout.hidden",
            AppSettings.View.ShowHiddenFiles, v => AppSettings.View.ShowHiddenFiles = v)),
        new("list", "pref.layout.dot", () => CheckRow("pref.layout.dot",
            AppSettings.View.ShowDotFiles, v => AppSettings.View.ShowDotFiles = v)),
        new("list", "pref.layout.pathHeader", () => CheckRow("pref.layout.pathHeader",
            AppSettings.View.ShowPathHeader, v => AppSettings.View.ShowPathHeader = v)),
        new("list", "pref.list.foldersFirst", () => CheckRow("pref.list.foldersFirst",
            AppSettings.Sort.FoldersFirst, v => AppSettings.Sort.FoldersFirst = v)),
        new("list", "pref.list.upNav", UpNavRadios),
        new("list", "pref.list.typeahead", TypeAheadRadios),
        new("list", "pref.list.typeaheadTimeout", () => NumberRow("pref.list.typeaheadTimeout",
            AppSettings.View.TypeAheadTimeoutMs, 200, 5000, 100, v => AppSettings.View.TypeAheadTimeoutMs = (long)v)),
        new("list", "pref.list.hudPos", HudPositionPicker),
        new("list", "pref.list.taSpecial", () => CheckRow("pref.list.taSpecial",
            AppSettings.View.TypeAheadSpecialChars, v => AppSettings.View.TypeAheadSpecialChars = v)),
        new("list", "pref.list.taSpace", () => CheckRow("pref.list.taSpace",
            AppSettings.View.TypeAheadSpace, v => AppSettings.View.TypeAheadSpace = v)),
        new("list", "pref.list.taBackspace", () => CheckRow("pref.list.taBackspace",
            AppSettings.View.TypeAheadBackspace, v => AppSettings.View.TypeAheadBackspace = v)),
        // 탭
        new("tabs", "pref.tabs.doubleClick", TabDoubleClickCombo),
        // 도구 모음(docs/44) — 그룹/항목 순서 편집기
        new("toolbar", "pref.toolbar.title", ToolbarOrderEditor),
        // 터미널(BP-T) — 긴 출력 처리(줄바꿈/최대 길이)
        new("terminal", "pref.term.longOutput", TerminalWrapEditor),
        // 파일 작업
        new("ops", "pref.layout.autoClose", () => CheckRow("pref.layout.autoClose",
            AppSettings.View.AutoCloseTransferWindow, v => AppSettings.View.AutoCloseTransferWindow = v)),
        new("ops", "pref.ops.sysClipboard", () => CheckRow("pref.ops.sysClipboard",
            AppSettings.View.UseSystemClipboard, v => AppSettings.View.UseSystemClipboard = v)),
        new("ops", "pref.ops.tabDwell", () => NumberRow("pref.ops.tabDwell",
            AppSettings.View.TabDwellMs, 500, 10000, 100, v => AppSettings.View.TabDwellMs = (int)v)),
        new("ops", "pref.ops.folderDwell", () => NumberRow("pref.ops.folderDwell",
            AppSettings.View.FolderDwellMs, 500, 10000, 100, v => AppSettings.View.FolderDwellMs = (int)v)),
        // 컨텍스트 메뉴(docs/38 §7)
        new("menu", "pref.menu.onTop", () => CheckRow("pref.menu.onTop",
            AppSettings.Menu.CustomSectionOnTop, v => AppSettings.Menu.CustomSectionOnTop = v)),
        new("menu", "pref.menu.itemsHeader", MenuItemsList),
        // 언어(i18n, docs/42)
        new("lang", "pref.lang.title", LanguageRadios),
    };

    // ── 검색 일치 판정 ───────────────────────────────────────────────
    private static bool Match(string label, string query) =>
        label.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>설정 항목 일치 = 제목 또는 설명에 검색어 포함(설정값 컨트롤은 판정 제외).</summary>
    private bool EntryMatches(Entry e, string query) =>
        Match(Loc.T(e.LabelKey), query) || (e.DescKey is not null && Match(Loc.T(e.DescKey), query));

    /// <summary>카테고리 표시 여부 = 그룹명 일치 <b>또는</b> 소속 항목(자식 카테고리 포함) 중 1개라도 일치.</summary>
    private bool CategoryVisible(Category cat, string query) =>
        query.Length == 0
        || Match(Loc.T(cat.LabelKey), query)
        || _entries.Any(e => e.CategoryKey == cat.Key && EntryMatches(e, query))
        || _categories.Any(c => c.Parent == cat.Key && CategoryVisible(c, query));

    /// <summary>카테고리 안에서 보여줄 항목 — 검색 없으면 전부, 검색 중이면 그룹명 일치 시 전부/아니면 일치 항목만.</summary>
    private IEnumerable<Entry> VisibleEntries(Category cat)
    {
        var own = _entries.Where(e => e.CategoryKey == cat.Key);
        return _query.Length == 0 || Match(Loc.T(cat.LabelKey), _query)
            ? own
            : own.Where(e => EntryMatches(e, _query));
    }

    // ── 트리(카테고리) — 검색어로 그룹 필터(일치 그룹만 표시·전개) ─────
    private void BuildTree()
    {
        _nodeKeys.Clear();
        NavTree.RootNodes.Clear();
        foreach (var cat in _categories.Where(c => c.Parent is null && CategoryVisible(c, _query)))
        {
            var node = new TreeViewNode { Content = Loc.T(cat.LabelKey), IsExpanded = true };
            _nodeKeys[node] = cat.Key;
            foreach (var child in _categories.Where(c => c.Parent == cat.Key && CategoryVisible(c, _query)))
            {
                var childNode = new TreeViewNode { Content = Loc.T(child.LabelKey) };
                _nodeKeys[childNode] = child.Key;
                node.Children.Add(childNode);
            }
            NavTree.RootNodes.Add(node);
        }
    }

    private void OnNavInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && _nodeKeys.TryGetValue(node, out string? key))
        {
            ShowCategory(key);   // 검색 중이면 그 카테고리의 일치 항목만(필터 유지)
        }
    }

    // ── 렌더(카테고리/검색) ──────────────────────────────────────────
    private void ShowCategory(string key)
    {
        _currentCategory = key;
        PageHost.Children.Clear();
        _building = true;
        RenderCategory(key);
        _building = false;
    }

    /// <summary>카테고리 1개를 헤더+항목으로 그린다. 자식 카테고리는 소제목으로 이어 그린다(VS Code식 섹션).
    /// 검색 중이면 제목/설명 일치 항목만(그룹명 일치 그룹은 전체) — 안내문(Note)은 검색 없을 때만.</summary>
    private void RenderCategory(string key)
    {
        var cat = _categories.First(c => c.Key == key);
        PageHost.Children.Add(Header(cat.LabelKey));
        foreach (var e in VisibleEntries(cat))
        {
            PageHost.Children.Add(e.Build());
        }
        if (cat.NoteKey is not null && _query.Length == 0)
        {
            PageHost.Children.Add(Note(cat.NoteKey));
        }
        foreach (var child in _categories.Where(c => c.Parent == key && CategoryVisible(c, _query)))
        {
            PageHost.Children.Add(SubHeader(child.LabelKey));
            foreach (var e in VisibleEntries(child))
            {
                PageHost.Children.Add(e.Build());
            }
            if (child.NoteKey is not null && _query.Length == 0)
            {
                PageHost.Children.Add(Note(child.NoteKey));
            }
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _query = sender.Text.Trim();
        BuildTree();   // 좌측 그룹 목록 필터(그룹명 일치 또는 일치 항목 보유)
        if (_query.Length == 0)
        {
            ShowCategory(_currentCategory);
            return;
        }
        // 우측: 전 카테고리에서 제목/설명 일치 항목을 "상위 › 그룹" 캡션과 함께 나열.
        PageHost.Children.Clear();
        _building = true;
        bool any = false;
        foreach (var cat in _categories)
        {
            var hits = _entries.Where(e => e.CategoryKey == cat.Key
                && (Match(Loc.T(cat.LabelKey), _query) || EntryMatches(e, _query))).ToList();
            if (hits.Count == 0)
            {
                continue;
            }
            any = true;
            PageHost.Children.Add(new TextBlock
            {
                Text = CategoryPath(cat),
                Opacity = 0.55,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
            });
            foreach (var e in hits)
            {
                PageHost.Children.Add(e.Build());
            }
        }
        if (!any)
        {
            PageHost.Children.Add(Note("pref.search.empty"));
        }
        _building = false;
    }

    /// <summary>검색 결과 캡션용 "상위 › 카테고리" 경로 라벨.</summary>
    private string CategoryPath(Category cat) =>
        cat.Parent is string p
            ? $"{Loc.T(_categories.First(c => c.Key == p).LabelKey)} › {Loc.T(cat.LabelKey)}"
            : Loc.T(cat.LabelKey);

    // ── 공통 빌더 ────────────────────────────────────────────────────
    private static TextBlock Header(string key) => new()
    {
        Text = Loc.T(key),
        FontSize = 18,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static TextBlock SubHeader(string key) => new()
    {
        Text = Loc.T(key),
        FontSize = 15,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 14, 0, 2),
    };

    private static TextBlock Note(string key) => new()
    {
        Text = Loc.T(key),
        Opacity = 0.5,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 0),
    };

    private CheckBox CheckRow(string labelKey, bool value, Action<bool> set)
    {
        var cb = new CheckBox { Content = Loc.T(labelKey), IsChecked = value, MinHeight = 0 };
        cb.Checked += (_, _) => { set(true); Changed(); };
        cb.Unchecked += (_, _) => { set(false); Changed(); };
        return cb;
    }

    /// <summary>라벨 + 라디오 묶음(세로). options = (라벨키, 현재 선택, 선택 시 동작).</summary>
    private FrameworkElement RadioColumn(string labelKey, string groupName,
        IEnumerable<(string LabelKey, bool Checked, Action Select)> options)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = Loc.T(labelKey), Opacity = 0.7 });
        foreach (var (key, isChecked, select) in options)
        {
            var rb = new RadioButton { Content = Loc.T(key), GroupName = groupName, IsChecked = isChecked, MinHeight = 0 };
            rb.Checked += (_, _) => { select(); Changed(); };
            panel.Children.Add(rb);
        }
        return panel;
    }

    private FrameworkElement NumberRow(string labelKey, double value, double min, double max, double step, Action<double> set)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var box = new NumberBox
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            SmallChange = step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 140,
        };
        box.ValueChanged += (_, a) =>
        {
            if (!double.IsNaN(a.NewValue))
            {
                set(Math.Clamp(a.NewValue, min, max));
                Changed();
            }
        };
        panel.Children.Add(box);
        panel.Children.Add(new TextBlock { Text = Loc.T(labelKey), Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    // ── 모양 › 테마 ──────────────────────────────────────────────────
    private FrameworkElement ThemeRadios() => RadioColumn("pref.appearance.theme", "ThemeMode", new (string, bool, Action)[]
    {
        ("pref.theme.system", AppSettings.Theme.Mode == AppThemeMode.System, () => AppSettings.Theme.Mode = AppThemeMode.System),
        ("pref.theme.light", AppSettings.Theme.Mode == AppThemeMode.Light, () => AppSettings.Theme.Mode = AppThemeMode.Light),
        ("pref.theme.dark", AppSettings.Theme.Mode == AppThemeMode.Dark, () => AppSettings.Theme.Mode = AppThemeMode.Dark),
    });

    // ── 모양 › 글꼴(PREF-3) ─────────────────────────────────────────
    /// <summary>
    /// 글꼴 슬롯 1개 — 라벨·설명 + <b>편집 가능 콤보</b>(설치 글꼴 목록 선택/직접 입력) + 크기.
    /// 입력 적용 시점 = 엔터(TextSubmitted)·포커스 이탈·목록 선택. 설치되지 않은 글꼴(쉼표 목록의
    /// 각 토큰 검사)은 <b>적용하지 않고 경고</b>를 표시한다(InstalledFonts.FirstMissing).
    /// </summary>
    private FrameworkElement FontRow(string labelKey, string descKey,
        Func<string> getFamily, Action<string> setFamily, Func<double> getSize, Action<double> setSize)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = Loc.T(labelKey), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = Loc.T(descKey), Opacity = 0.55, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        var warn = new TextBlock
        {
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        var combo = new ComboBox
        {
            IsEditable = true,
            Width = 280,
            ItemsSource = InstalledFonts.Families(),
        };
        // 편집 콤보는 템플릿 로드 전 Text 설정이 무시돼 빈칸으로 보인다(클릭해야 값 표시) →
        // ① 목록에 있는 값이면 SelectedItem으로(즉시 표시) ② 쉼표 목록 등은 Loaded 후 Text 재설정.
        string current = getFamily();
        if (InstalledFonts.Families().FirstOrDefault(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)) is string exact)
        {
            combo.SelectedItem = exact;
        }
        combo.Loaded += (_, _) =>
        {
            if (combo.Text != getFamily())
            {
                combo.Text = getFamily();
            }
        };
        void Apply(string text)
        {
            string? missing = InstalledFonts.FirstMissing(text);
            if (missing is null)
            {
                warn.Visibility = Visibility.Collapsed;
                string value = text.Trim();
                if (value != getFamily())
                {
                    setFamily(value);
                    Changed();
                }
            }
            else
            {
                warn.Text = Loc.T("pref.fonts.invalid", missing.Length == 0 ? "—" : missing);
                warn.Visibility = Visibility.Visible;
            }
        }
        combo.TextSubmitted += (_, args) => { Apply(args.Text); args.Handled = true; };   // 엔터
        combo.LostFocus += (_, _) => Apply(combo.Text);                                    // 포커스 이탈
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string s) { Apply(s); } };   // 목록 선택

        var size = new NumberBox
        {
            Value = getSize(),
            Minimum = 8,
            Maximum = 32,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 120,
        };
        ToolTipService.SetToolTip(size, Loc.T("pref.fonts.size"));
        size.ValueChanged += (_, a) =>
        {
            if (!double.IsNaN(a.NewValue))
            {
                setSize(Math.Clamp(a.NewValue, 8, 32));
                Changed();
            }
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(combo);
        row.Children.Add(size);
        panel.Children.Add(row);
        panel.Children.Add(warn);
        return panel;
    }

    /// <summary>"파일 헤더 글꼴" — 글꼴/크기는 파일 목록을 따르고 꾸미기(두껍게/기울임)만 지정.</summary>
    private FrameworkElement HeaderFontRow()
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = Loc.T("pref.fonts.header"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = Loc.T("pref.fonts.headerDesc"), Opacity = 0.55, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        row.Children.Add(CheckRow("pref.fonts.bold", AppSettings.Fonts.HeaderBold, v => AppSettings.Fonts.HeaderBold = v));
        row.Children.Add(CheckRow("pref.fonts.italic", AppSettings.Fonts.HeaderItalic, v => AppSettings.Fonts.HeaderItalic = v));
        panel.Children.Add(row);
        return panel;
    }

    // ── 파일 목록 ────────────────────────────────────────────────────
    private FrameworkElement UpNavRadios() => RadioColumn("pref.list.upNav", "UpNavAlign", new (string, bool, Action)[]
    {
        ("pref.list.upNav.top", Math.Abs(AppSettings.View.UpNavTargetAlign - 0) < 0.01, () => AppSettings.View.UpNavTargetAlign = 0),
        ("pref.list.upNav.center", Math.Abs(AppSettings.View.UpNavTargetAlign - 0.5) < 0.01, () => AppSettings.View.UpNavTargetAlign = 0.5),
        ("pref.list.upNav.bottom", Math.Abs(AppSettings.View.UpNavTargetAlign - 1) < 0.01, () => AppSettings.View.UpNavTargetAlign = 1),
    });

    private FrameworkElement TypeAheadRadios() => RadioColumn("pref.list.typeahead", "TypeAheadScope", new (string, bool, Action)[]
    {
        ("pref.list.typeahead.global", AppSettings.View.TypeAheadScope == 0, () => AppSettings.View.TypeAheadScope = 0),
        ("pref.list.typeahead.current", AppSettings.View.TypeAheadScope == 1, () => AppSettings.View.TypeAheadScope = 1),
        ("pref.list.typeahead.visible", AppSettings.View.TypeAheadScope == 2, () => AppSettings.View.TypeAheadScope = 2),
    });

    /// <summary>타입어헤드 검색어 HUD 위치(3×3) — <b>매트릭스 타일 피커</b>: 각 타일은 미니 패널 안의
    /// 배지 점 위치를 그대로 보여주고(머티리얼풍 라운드·강조색 선택), 클릭으로 선택·즉시 반영.</summary>
    private FrameworkElement HudPositionPicker()
    {
        var root = new StackPanel { Spacing = 6 };
        root.Children.Add(new TextBlock { Text = Loc.T("pref.list.hudPos"), Opacity = 0.7 });

        var accent = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3D, 0x8B, 0xFF));      // NexaAccent
        var accentSoft = new SolidColorBrush(Windows.UI.Color.FromArgb(0x2E, 0x3D, 0x8B, 0xFF));
        var idleBorder = new SolidColorBrush(Windows.UI.Color.FromArgb(0x55, 0x80, 0x80, 0x80));
        var idleDot = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0x80, 0x80, 0x80));

        string[] tipKeys =
        {
            "pos.topLeft", "pos.top", "pos.topRight",
            "pos.left", "pos.center", "pos.right",
            "pos.bottomLeft", "pos.bottom", "pos.bottomRight",
        };
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        for (int i = 0; i < 3; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        var tiles = new (Button Tile, Border Dot)[9];
        void Refresh()
        {
            int sel = (int)AppSettings.View.TypeAheadHudPosition;
            for (int i = 0; i < 9; i++)
            {
                bool on = i == sel;
                tiles[i].Tile.Background = on ? accentSoft : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                tiles[i].Tile.BorderBrush = on ? accent : idleBorder;
                tiles[i].Dot.Background = on ? accent : idleDot;
            }
        }

        for (int i = 0; i < 9; i++)
        {
            int idx = i;
            var dot = new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(2.5),
                Margin = new Thickness(4),
                HorizontalAlignment = (i % 3) switch { 0 => HorizontalAlignment.Left, 1 => HorizontalAlignment.Center, _ => HorizontalAlignment.Right },
                VerticalAlignment = (i / 3) switch { 0 => VerticalAlignment.Top, 1 => VerticalAlignment.Center, _ => VerticalAlignment.Bottom },
            };
            var tile = new Button
            {
                Width = 48,
                Height = 34,
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = dot,
            };
            ToolTipService.SetToolTip(tile, Loc.T(tipKeys[i]));
            tile.Click += (_, _) =>
            {
                AppSettings.View.TypeAheadHudPosition = (HudPosition)idx;
                Refresh();
                Changed();
            };
            Grid.SetRow(tile, i / 3);
            Grid.SetColumn(tile, i % 3);
            grid.Children.Add(tile);
            tiles[i] = (tile, dot);
        }
        Refresh();
        root.Children.Add(grid);
        return root;
    }

    // ── 탭 ───────────────────────────────────────────────────────────
    private FrameworkElement TabDoubleClickCombo()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var combo = new ComboBox
        {
            ItemsSource = new[]
            {
                Loc.T("pref.tabs.dc.none"),
                Loc.T("pref.tabs.dc.close"),
                Loc.T("pref.tabs.dc.favorite"),
                Loc.T("pref.tabs.dc.popup"),
            },
            SelectedIndex = (int)AppSettings.Tab.DoubleClick,
            Width = 220,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                AppSettings.Tab.DoubleClick = (TabDoubleClickAction)combo.SelectedIndex;
                Changed();
            }
        };
        panel.Children.Add(combo);
        panel.Children.Add(new TextBlock { Text = Loc.T("pref.tabs.doubleClick"), Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    // ── 도구 모음(docs/44) — 그룹/그룹 내 항목 순서 ▲▼ 편집 ───────────
    /// <summary>도구 모음 순서 편집기 — 그룹(굵게)과 그 항목(들여쓰기)을 ▲/▼로 이동.
    /// 이동 즉시 <see cref="AppSettings.Toolbar"/>에 반영·저장(Changed)하고 페이지를 다시 그린다.</summary>
    private FrameworkElement ToolbarOrderEditor()
    {
        var panel = new StackPanel { Spacing = 2 };
        var catalog = MainWindow.ToolbarCatalog();
        var groupIds = OrderedIds(catalog.Select(g => g.Id).ToList(), AppSettings.Toolbar.GroupOrder);
        for (int gi = 0; gi < groupIds.Count; gi++)
        {
            var group = catalog.First(g => g.Id == groupIds[gi]);
            int captured = gi;
            panel.Children.Add(OrderRow(Loc.T(group.LabelKey), bold: true, indent: 0,
                canUp: gi > 0, canDown: gi < groupIds.Count - 1,
                move: delta => MoveGroup(groupIds, captured, delta)));
            AppSettings.Toolbar.ItemOrder.TryGetValue(group.Id, out var itemOrder);
            var itemIds = OrderedIds(group.Items.Select(i => i.Id).ToList(), itemOrder);
            for (int ii = 0; ii < itemIds.Count; ii++)
            {
                var item = group.Items.First(i => i.Id == itemIds[ii]);
                int gIdx = ii;
                string groupId = group.Id;
                var ids = itemIds;
                panel.Children.Add(OrderRow(Loc.T(item.LabelKey), bold: false, indent: 24,
                    canUp: ii > 0, canDown: ii < itemIds.Count - 1,
                    move: delta => MoveItem(groupId, ids, gIdx, delta)));
            }
        }
        return panel;
    }

    /// <summary>순서 재정의 적용 — 나열 id 먼저(그 순서), 미기재 id는 기본 순서 유지로 뒤에(MainWindow와 동일 규칙).</summary>
    private static List<string> OrderedIds(List<string> defaults, List<string>? order)
    {
        if (order is null || order.Count == 0)
        {
            return defaults;
        }
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < order.Count; i++)
        {
            rank[order[i]] = i;
        }
        return defaults
            .Select((d, i) => (d, key: rank.TryGetValue(d, out int k) ? k : order.Count + i))
            .OrderBy(t => t.key)
            .Select(t => t.d)
            .ToList();
    }

    private void MoveGroup(List<string> ids, int index, int delta)
    {
        int to = index + delta;
        if (to < 0 || to >= ids.Count)
        {
            return;
        }
        (ids[index], ids[to]) = (ids[to], ids[index]);
        AppSettings.Toolbar.GroupOrder.Clear();
        AppSettings.Toolbar.GroupOrder.AddRange(ids);
        Changed();
        ShowCategory("toolbar");   // 새 순서로 편집기 다시 그림
    }

    private void MoveItem(string groupId, List<string> ids, int index, int delta)
    {
        int to = index + delta;
        if (to < 0 || to >= ids.Count)
        {
            return;
        }
        (ids[index], ids[to]) = (ids[to], ids[index]);
        AppSettings.Toolbar.ItemOrder[groupId] = new List<string>(ids);
        Changed();
        ShowCategory("toolbar");
    }

    /// <summary>순서 편집 행 — [▲][▼] + 라벨(그룹=굵게, 항목=들여쓰기).</summary>
    private FrameworkElement OrderRow(string label, bool bold, double indent, bool canUp, bool canDown, Action<int> move)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(indent, 0, 0, 0) };
        row.Children.Add(MoveButton("", canUp, () => move(-1)));    // ChevronUp
        row.Children.Add(MoveButton("", canDown, () => move(+1))); // ChevronDown
        row.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        });
        return row;
    }

    private static Button MoveButton(string glyph, bool enabled, Action onClick)
    {
        var btn = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 10 },
            Padding = new Thickness(5, 3, 5, 3),
            MinWidth = 0,
            MinHeight = 0,
            IsEnabled = enabled,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    // ── 터미널(BP-T) — 긴 출력 처리 ──────────────────────────────────
    /// <summary>줄바꿈(뷰포트 폭) ↔ 최대 길이까지 줄바꿈 없음(가로 스크롤) + 최대 글자 수 입력.</summary>
    private FrameworkElement TerminalWrapEditor()
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = Loc.T("pref.term.longOutput"), Opacity = 0.7 });

        var cols = new NumberBox
        {
            Value = AppSettings.Terminal.MaxColumns,
            Minimum = 80,
            Maximum = 1000,
            SmallChange = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 140,
            IsEnabled = AppSettings.Terminal.NoWrap,
        };
        var wrap = new RadioButton { Content = Loc.T("pref.term.wrap"), GroupName = "TermWrap", IsChecked = !AppSettings.Terminal.NoWrap, MinHeight = 0 };
        var noWrap = new RadioButton { Content = Loc.T("pref.term.noWrap"), GroupName = "TermWrap", IsChecked = AppSettings.Terminal.NoWrap, MinHeight = 0 };
        wrap.Checked += (_, _) => { AppSettings.Terminal.NoWrap = false; cols.IsEnabled = false; Changed(); };
        noWrap.Checked += (_, _) => { AppSettings.Terminal.NoWrap = true; cols.IsEnabled = true; Changed(); };
        panel.Children.Add(wrap);
        panel.Children.Add(noWrap);

        var colsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(24, 0, 0, 0) };
        cols.ValueChanged += (_, a) =>
        {
            if (!double.IsNaN(a.NewValue))
            {
                AppSettings.Terminal.MaxColumns = (int)Math.Clamp(a.NewValue, 80, 1000);
                Changed();
            }
        };
        colsRow.Children.Add(cols);
        colsRow.Children.Add(new TextBlock { Text = Loc.T("pref.term.maxCols"), Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(colsRow);
        return panel;
    }

    // ── 컨텍스트 메뉴(커스텀 항목 표시/숨김, docs/38 §7) ──────────────
    private FrameworkElement MenuItemsList()
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = Loc.T("pref.menu.itemsHeader"), Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0) });
        foreach (var (id, label) in MainWindow.CustomMenuItemCatalog())
        {
            string capturedId = id;
            var cb = new CheckBox { Content = label, IsChecked = !AppSettings.Menu.DisabledItems.Contains(capturedId), MinHeight = 0 };
            cb.Checked += (_, _) => { AppSettings.Menu.DisabledItems.Remove(capturedId); Changed(); };
            cb.Unchecked += (_, _) => { AppSettings.Menu.DisabledItems.Add(capturedId); Changed(); };
            panel.Children.Add(cb);
        }
        return panel;
    }

    // ── 언어(i18n, D-2/PREF-8, docs/42) — 발견된 .lang 파일에서 동적 구성 ─
    private FrameworkElement LanguageRadios()
    {
        var options = new List<(string Label, string Culture)> { (Loc.T("pref.lang.system"), "") };
        foreach (LangInfo info in LangCatalog.Discover())
        {
            string label = string.IsNullOrEmpty(info.NameEn) || info.NameEn == info.Name
                ? info.Name
                : $"{info.Name} ({info.NameEn})";
            options.Add((label, info.Code));
        }
        var panel = new StackPanel { Spacing = 2 };
        foreach (var (label, culture) in options)
        {
            var rb = new RadioButton { Content = label, GroupName = "Culture", IsChecked = AppSettings.General.Culture == culture, MinHeight = 0 };
            string captured = culture;
            rb.Checked += (_, _) => { AppSettings.General.Culture = captured; Changed(); };
            panel.Children.Add(rb);
        }
        return panel;
    }
}
