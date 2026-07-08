using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Nexa.App;

/// <summary>
/// 통합 설정 창(docs/40 PREF-1) — 페이지식(모양·레이아웃·메뉴·언어…). 각 컨트롤은 <see cref="AppSettings"/>를
/// 직접 편집하고, 변경 시 호스트(<see cref="MainWindow.OnPreferencesChanged"/>)가 라이브 적용 + settings.json 저장.
/// 문자열은 <see cref="Loc"/>(i18n). 후속: 컬럼·단축키·런처·즐겨찾기 페이지(docs/40 §3).
/// </summary>
public sealed partial class PreferencesWindow : Window
{
    private readonly MainWindow _host;
    private bool _building;   // 초기 구성 중 이벤트 발화로 인한 저장 소음 방지

    internal PreferencesWindow(MainWindow host)
    {
        InitializeComponent();
        _host = host;
        Title = Loc.T("pref.title");
        AppWindow.Resize(new SizeInt32(720, 520));
        foreach (string key in new[] { "pref.page.appearance", "pref.page.layout", "pref.page.menu", "pref.page.language" })
        {
            PageList.Items.Add(new ListViewItem { Content = Loc.T(key) });
        }
        PageList.SelectionChanged += (_, _) => ShowPage(PageList.SelectedIndex);
        PageList.SelectedIndex = 0;
    }

    private void Changed()
    {
        if (!_building)
        {
            _host.OnPreferencesChanged();
        }
    }

    private void ShowPage(int index)
    {
        PageHost.Children.Clear();
        _building = true;
        switch (index)
        {
            case 0: BuildAppearance(); break;
            case 1: BuildLayout(); break;
            case 2: BuildMenu(); break;
            case 3: BuildLanguage(); break;
        }
        _building = false;
    }

    // ── 페이지 헬퍼 ──────────────────────────────────────────────────
    private void Header(string key) =>
        PageHost.Children.Add(new TextBlock { Text = Loc.T(key), FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

    private void Note(string key) =>
        PageHost.Children.Add(new TextBlock { Text = Loc.T(key), Opacity = 0.5, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });

    private void Check(string labelKey, bool value, Action<bool> set)
    {
        var cb = new CheckBox { Content = Loc.T(labelKey), IsChecked = value };
        cb.Checked += (_, _) => { set(true); Changed(); };
        cb.Unchecked += (_, _) => { set(false); Changed(); };
        PageHost.Children.Add(cb);
    }

    // ── 모양 ─────────────────────────────────────────────────────────
    private void BuildAppearance()
    {
        Header("pref.page.appearance");
        PageHost.Children.Add(new TextBlock { Text = Loc.T("pref.appearance.theme"), Opacity = 0.7 });
        AddThemeRadio("pref.theme.system", AppThemeMode.System);
        AddThemeRadio("pref.theme.light", AppThemeMode.Light);
        AddThemeRadio("pref.theme.dark", AppThemeMode.Dark);
        Note("pref.appearance.note");
    }

    private void AddThemeRadio(string labelKey, AppThemeMode mode)
    {
        var rb = new RadioButton { Content = Loc.T(labelKey), GroupName = "ThemeMode", IsChecked = AppSettings.Theme.Mode == mode };
        rb.Checked += (_, _) => { AppSettings.Theme.Mode = mode; Changed(); };
        PageHost.Children.Add(rb);
    }

    // ── 레이아웃 ─────────────────────────────────────────────────────
    private void BuildLayout()
    {
        Header("pref.page.layout");
        Check("pref.layout.hidden", AppSettings.View.ShowHiddenFiles, v => AppSettings.View.ShowHiddenFiles = v);
        Check("pref.layout.dot", AppSettings.View.ShowDotFiles, v => AppSettings.View.ShowDotFiles = v);
        Check("pref.layout.pathHeader", AppSettings.View.ShowPathHeader, v => AppSettings.View.ShowPathHeader = v);
        Check("pref.layout.autoClose", AppSettings.View.AutoCloseTransferWindow, v => AppSettings.View.AutoCloseTransferWindow = v);
        Note("pref.layout.note");
    }

    // ── 메뉴(컨텍스트 메뉴 커스텀 항목, docs/38 §7) ──────────────────
    private void BuildMenu()
    {
        Header("pref.menu.title");
        Check("pref.menu.onTop", AppSettings.Menu.CustomSectionOnTop, v => AppSettings.Menu.CustomSectionOnTop = v);
        PageHost.Children.Add(new TextBlock { Text = Loc.T("pref.menu.itemsHeader"), Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var (id, label) in MainWindow.CustomMenuItemCatalog())
        {
            string capturedId = id;
            var cb = new CheckBox { Content = label, IsChecked = !AppSettings.Menu.DisabledItems.Contains(capturedId) };
            cb.Checked += (_, _) => { AppSettings.Menu.DisabledItems.Remove(capturedId); Changed(); };
            cb.Unchecked += (_, _) => { AppSettings.Menu.DisabledItems.Add(capturedId); Changed(); };
            PageHost.Children.Add(cb);
        }
        Note("pref.menu.note");
    }

    // ── 언어(i18n, D-2/PREF-8) ───────────────────────────────────────
    private void BuildLanguage()
    {
        Header("pref.lang.title");
        AddLangRadio("pref.lang.system", "");
        AddLangRadio("pref.lang.ko", "ko");
        AddLangRadio("pref.lang.en", "en");
        Note("pref.lang.restartNote");
    }

    private void AddLangRadio(string labelKey, string culture)
    {
        var rb = new RadioButton { Content = Loc.T(labelKey), GroupName = "Culture", IsChecked = AppSettings.General.Culture == culture };
        rb.Checked += (_, _) => { AppSettings.General.Culture = culture; Changed(); };
        PageHost.Children.Add(rb);
    }
}
