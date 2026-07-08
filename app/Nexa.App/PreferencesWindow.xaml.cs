using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Nexa.App;

/// <summary>
/// 통합 설정 창(docs/40 PREF-1) — 페이지식(모양·레이아웃·메뉴…). 각 컨트롤은 <see cref="AppSettings"/>를
/// 직접 편집하고, 변경 시 호스트(<see cref="MainWindow.OnPreferencesChanged"/>)가 라이브 적용 + settings.json 저장.
/// 후속: 컬럼·단축키·런처·즐겨찾기·언어 페이지(docs/40 §3), 테마팩/폰트/밀도.
/// </summary>
public sealed partial class PreferencesWindow : Window
{
    private readonly MainWindow _host;
    private bool _building;   // 초기 구성 중 이벤트 발화로 인한 저장 소음 방지

    internal PreferencesWindow(MainWindow host)
    {
        InitializeComponent();
        _host = host;
        Title = "설정";
        AppWindow.Resize(new SizeInt32(720, 520));
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
        }
        _building = false;
    }

    // ── 페이지 헬퍼 ──────────────────────────────────────────────────
    private void Header(string text) =>
        PageHost.Children.Add(new TextBlock { Text = text, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

    private CheckBox Check(string label, bool value, Action<bool> set)
    {
        var cb = new CheckBox { Content = label, IsChecked = value };
        cb.Checked += (_, _) => { set(true); Changed(); };
        cb.Unchecked += (_, _) => { set(false); Changed(); };
        PageHost.Children.Add(cb);
        return cb;
    }

    // ── 모양 ─────────────────────────────────────────────────────────
    private void BuildAppearance()
    {
        Header("모양");
        PageHost.Children.Add(new TextBlock { Text = "테마", Opacity = 0.7 });
        AddThemeRadio("시스템(OS 설정 추종)", AppThemeMode.System);
        AddThemeRadio("라이트", AppThemeMode.Light);
        AddThemeRadio("다크", AppThemeMode.Dark);
        PageHost.Children.Add(new TextBlock
        {
            Text = "테마팩(색)·폰트·밀도 설정은 후속(docs/39 §5).",
            Opacity = 0.5, FontSize = 12, Margin = new Thickness(0, 8, 0, 0),
        });
    }

    private void AddThemeRadio(string label, AppThemeMode mode)
    {
        var rb = new RadioButton
        {
            Content = label,
            GroupName = "ThemeMode",
            IsChecked = AppSettings.Theme.Mode == mode,
        };
        rb.Checked += (_, _) => { AppSettings.Theme.Mode = mode; Changed(); };
        PageHost.Children.Add(rb);
    }

    // ── 레이아웃 ─────────────────────────────────────────────────────
    private void BuildLayout()
    {
        Header("레이아웃");
        Check("숨김 파일 보기", AppSettings.View.ShowHiddenFiles, v => AppSettings.View.ShowHiddenFiles = v);
        Check("점(.) 파일 보기", AppSettings.View.ShowDotFiles, v => AppSettings.View.ShowDotFiles = v);
        Check("경로·항목 수 헤더 보기", AppSettings.View.ShowPathHeader, v => AppSettings.View.ShowPathHeader = v);
        Check("전송 완료 창 자동 닫기(2초)", AppSettings.View.AutoCloseTransferWindow, v => AppSettings.View.AutoCloseTransferWindow = v);
        PageHost.Children.Add(new TextBlock
        {
            Text = "패널/런처/하단 표시는 표시(S) 메뉴에서, 이관은 후속(PREF-3).",
            Opacity = 0.5, FontSize = 12, Margin = new Thickness(0, 8, 0, 0),
        });
    }

    // ── 메뉴(컨텍스트 메뉴 커스텀 항목, docs/38 §7) ──────────────────
    private void BuildMenu()
    {
        Header("컨텍스트 메뉴");
        Check("커스텀 항목을 셸 항목 위에 표시", AppSettings.Menu.CustomSectionOnTop, v => AppSettings.Menu.CustomSectionOnTop = v);
        PageHost.Children.Add(new TextBlock { Text = "표시할 커스텀 항목", Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var (id, label) in MainWindow.CustomMenuItemCatalog())
        {
            string capturedId = id;
            var cb = new CheckBox { Content = label, IsChecked = !AppSettings.Menu.DisabledItems.Contains(capturedId) };
            cb.Checked += (_, _) => { AppSettings.Menu.DisabledItems.Remove(capturedId); Changed(); };
            cb.Unchecked += (_, _) => { AppSettings.Menu.DisabledItems.Add(capturedId); Changed(); };
            PageHost.Children.Add(cb);
        }
        PageHost.Children.Add(new TextBlock
        {
            Text = "순서 변경·사용자 정의 항목은 후속(docs/38 §7-3).",
            Opacity = 0.5, FontSize = 12, Margin = new Thickness(0, 8, 0, 0),
        });
    }
}
