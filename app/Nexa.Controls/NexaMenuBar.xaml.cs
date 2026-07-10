using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace Nexa.Controls;

/// <summary>
/// 초박형 커스텀 메뉴 바(재사용 컨트롤). 최상위 메뉴는 <see cref="Button"/>, 드롭다운은 경량 <see cref="Popup"/>(사각 Border).
/// 기본 <c>MenuBar</c>/<c>MenuFlyout</c>의 한계(초박형 불가·둥근 팝업·hover 전환 불가)를 우회하며,
/// <b>hover 전환</b>(열린 상태에서 다른 메뉴로 이동 시 자동 오픈) · <b>ALT 토글/단축키</b> · <b>사각 클래식 모양</b>을 구현한다.
/// </summary>
public sealed partial class NexaMenuBar : UserControl
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    // 색은 호스트 앱 테마 토큰(NexaMenu*, App.xaml ThemeDictionaries)에서 ActualTheme 기준으로 조회 —
    // 다크 모드에서 어두운 텍스트가 안 보이던 하드코딩 제거. 토큰이 없으면 라이트 기본값 폴백.
    private Brush MenuTextBrush => ThemeBrush("NexaMenuTextBrush", Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
    private Brush MenuHoverBrush => ThemeBrush("NexaMenuHoverBrush", Color.FromArgb(0xFF, 0xCC, 0xE4, 0xF7));

    /// <summary>호스트 앱 테마 사전에서 브러시 조회(현재 <see cref="FrameworkElement.ActualTheme"/> 기준).</summary>
    private Brush ThemeBrush(string key, Color fallback)
    {
        string theme = ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(theme, out object? dict)
            && dict is ResourceDictionary rd && rd.TryGetValue(key, out object? value) && value is Brush brush)
        {
            return brush;
        }
        return new SolidColorBrush(fallback);
    }

    private readonly List<Button> _headers = new();
    private readonly List<char> _mnemonics = new();   // 각 메뉴의 Alt 단축 문자(대문자), 없으면 '\0'

    private int _activeIndex = -1;   // 현재 열린 메뉴(-1=없음)
    private bool _altCombo;          // Alt와 함께 다른 키가 눌렸는가(단독 탭 판별)
    private UIElement? _root;        // 루트(전역 키/클릭 후킹 대상)

    public NexaMenuBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        // 라이트/다크 전환 시 헤더 색 재적용(드롭다운 항목은 열 때마다 생성이라 자동 반영).
        ActualThemeChanged += (_, _) => { CloseMenu(); if (IsLoaded) { BuildHeaders(); } };
    }

    /// <summary>최상위 메뉴 목록(XAML에서 <c>NexaMenuBar.Menus</c>로 채운다).</summary>
    public IList<NexaMenu> Menus { get; } = new List<NexaMenu>();

    /// <summary>메뉴 바(최상위 버튼) 높이(px).</summary>
    public static readonly DependencyProperty MenuHeightProperty =
        DependencyProperty.Register(
            nameof(MenuHeight), typeof(double), typeof(NexaMenuBar), new PropertyMetadata(22.0));

    public double MenuHeight
    {
        get => (double)GetValue(MenuHeightProperty);
        set => SetValue(MenuHeightProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildHeaders();
        // 전역 키(ALT 토글/단축키·ESC) + 바깥 클릭 닫기 후킹.
        if (XamlRoot?.Content is UIElement root)
        {
            _root = root;
            root.AddHandler(KeyDownEvent, new KeyEventHandler(OnRootKeyDown), true);
            root.AddHandler(KeyUpEvent, new KeyEventHandler(OnRootKeyUp), true);
            root.AddHandler(PointerPressedEvent, new PointerEventHandler(OnRootPointerPressed), true);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_root is not null)
        {
            _root.RemoveHandler(KeyDownEvent, new KeyEventHandler(OnRootKeyDown));
            _root.RemoveHandler(KeyUpEvent, new KeyEventHandler(OnRootKeyUp));
            _root.RemoveHandler(PointerPressedEvent, new PointerEventHandler(OnRootPointerPressed));
            _root = null;
        }
    }

    /// <summary>폰트(FontFamily/FontSize) 변경을 이미 생성된 헤더에 반영(설정 라이브 적용, PREF-3).
    /// 드롭다운 항목은 열 때마다 생성하므로 헤더만 재구성하면 된다.</summary>
    public void RefreshFonts()
    {
        if (IsLoaded)
        {
            CloseMenu();
            BuildHeaders();
        }
    }

    /// <summary>Menus로부터 최상위 헤더 버튼을 생성한다(드롭다운은 Popup으로 별도 표시).</summary>
    private void BuildHeaders()
    {
        Host.Children.Clear();
        _headers.Clear();
        _mnemonics.Clear();

        for (int i = 0; i < Menus.Count; i++)
        {
            var menu = Menus[i];
            int index = i;

            var btn = new Button
            {
                Content = menu.Header,
                FontSize = FontSize,
                FontFamily = FontFamily,
                Height = Math.Max(MenuHeight, FontSize + 6),   // 큰 글꼴 설정 시 클리핑 방지
                MinHeight = 0,
                MinWidth = 0,
                Padding = new Thickness(8, 0, 8, 0),
                Background = TransparentBrush,
                Foreground = MenuTextBrush,   // 테마 토큰(라이트=어두운색/다크=밝은 회색)
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            btn.Click += (_, _) => { if (_activeIndex == index) CloseMenu(); else OpenMenu(index); };
            btn.PointerEntered += (_, _) => { if (_activeIndex >= 0 && _activeIndex != index) OpenMenu(index); };

            _headers.Add(btn);
            _mnemonics.Add(ParseMnemonic(menu.Header));
            Host.Children.Add(btn);
        }
    }

    /// <summary>"파일(F)"에서 Alt 단축 문자 'F'를 추출(대문자). 없으면 '\0'.</summary>
    private static char ParseMnemonic(string header)
    {
        int open = header.IndexOf('(');
        if (open >= 0 && open + 1 < header.Length)
        {
            char c = header[open + 1];
            if (char.IsLetter(c))
            {
                return char.ToUpperInvariant(c);
            }
        }
        return '\0';
    }

    /// <summary>지정 메뉴를 헤더 아래에 사각 팝업으로 연다(항목 없으면 닫힘).</summary>
    private void OpenMenu(int index)
    {
        if (index < 0 || index >= Menus.Count)
        {
            CloseMenu();
            return;
        }
        var menu = Menus[index];
        if (menu.Items.Count == 0)
        {
            CloseMenu();
            SetActiveHeader(index);   // 항목 없어도 헤더는 활성 표시(트래킹 유지)
            _activeIndex = index;
            return;
        }
        // Popup 콘텐츠는 루트 RequestedTheme를 상속하지 않음 → 팝업의 ThemeResource가 현재 테마를 따르게 지정.
        PopupBorder.RequestedTheme = ActualTheme;

        // 항목 구성(사각 하이라이트 Border).
        ItemsHost.Children.Clear();
        foreach (var entry in menu.Items)
        {
            ItemsHost.Children.Add(CreateItem(entry));
        }

        // 헤더 좌측 하단 정렬로 위치.
        var btn = _headers[index];
        Point p = btn.TransformToVisual(this).TransformPoint(new Point(0, 0));
        MenuPopup.HorizontalOffset = p.X;
        MenuPopup.VerticalOffset = p.Y + btn.ActualHeight;
        MenuPopup.IsOpen = true;

        _activeIndex = index;
        SetActiveHeader(index);
    }

    private void CloseMenu()
    {
        MenuPopup.IsOpen = false;
        _activeIndex = -1;
        SetActiveHeader(-1);
    }

    /// <summary>활성 헤더만 배경 하이라이트(테마 토큰).</summary>
    private void SetActiveHeader(int index)
    {
        var active = MenuHoverBrush;
        for (int i = 0; i < _headers.Count; i++)
        {
            _headers[i].Background = i == index ? active : TransparentBrush;
        }
    }

    /// <summary>
    /// 드롭다운 항목 하나(사각, hover 하이라이트, 탭 시 닫힘).
    /// 체크형(<see cref="NexaMenuEntry.IsCheckable"/>)이면 좌측에 체크 칸을 두고 탭할 때마다 상태를 토글한다.
    /// 탭 시 <see cref="NexaMenuEntry.Click"/>을 발생시켜 호스트가 명령을 처리한다.
    /// </summary>
    private Border CreateItem(NexaMenuEntry entry)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var text = MenuTextBrush;    // 테마 토큰(항목은 열 때마다 생성 → 현재 테마 반영)
        var hover = MenuHoverBrush;

        // 체크 칸(18px)은 체크 가능 여부와 무관하게 모든 항목에 상시 예약(미체크/체크불가=투명) —
        // 항목 간 텍스트 시작 위치를 통일한다(체크 해제 시 왼쪽으로 붙던 정렬 문제).
        var check = new FontIcon
        {
            Glyph = "",   // Segoe MDL2 CheckMark
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = FontSize,
            Foreground = text,
            Width = 18,
            Opacity = entry.IsCheckable && entry.IsChecked ? 1 : 0,
        };
        content.Children.Add(check);

        content.Children.Add(new TextBlock
        {
            Text = entry.Text,
            FontSize = FontSize,
            FontFamily = FontFamily,
            Foreground = text,
        });

        var item = new Border
        {
            Padding = new Thickness(6, 4, 22, 4),   // 체크 칸 상시 예약 → 전 항목 동일 패딩(텍스트 정렬 통일)
            Background = TransparentBrush,
            Child = content,
        };
        item.PointerEntered += (_, _) => item.Background = hover;
        item.PointerExited += (_, _) => item.Background = TransparentBrush;
        item.Tapped += (_, _) =>
        {
            if (entry.IsCheckable)
            {
                entry.IsChecked = !entry.IsChecked;
                check.Opacity = entry.IsChecked ? 1 : 0;
            }
            entry.RaiseClick();
            CloseMenu();
        };
        return item;
    }

    // ── 전역 키/클릭 후킹 (ALT 토글·단축키, ESC, 바깥 클릭 닫기) ─────────

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Menu)
        {
            _altCombo = false;   // Alt 눌림 시작 — 단독 여부는 KeyUp에서 판정
            return;
        }
        if (e.Key == VirtualKey.Escape && _activeIndex >= 0)
        {
            CloseMenu();
            e.Handled = true;
            return;
        }

        // Alt+문자 단축(또는 메뉴 활성 상태에서 문자) → 해당 메뉴 열기.
        bool altDown = IsDown(VirtualKey.Menu);
        if (altDown)
        {
            _altCombo = true;
        }
        if (altDown || _activeIndex >= 0)
        {
            int idx = FindMnemonic(e.Key);
            if (idx >= 0)
            {
                OpenMenu(idx);
                e.Handled = true;
            }
        }
    }

    private void OnRootKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Menu)
        {
            // Alt 단독으로는 메뉴를 '열지' 않는다 — 표시는 Alt+문자 단축키(파일=Alt+F 등)로만.
            // 이미 열려 있으면 Alt로 닫기만 허용(ESC와 동일한 취소 경로).
            if (!_altCombo && _activeIndex >= 0)
            {
                CloseMenu();
                e.Handled = true;
            }
            _altCombo = false;
        }
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_activeIndex < 0)
        {
            return;
        }
        var src = e.OriginalSource as DependencyObject;
        if (!IsWithin(src, Host) && !IsWithin(src, PopupBorder))
        {
            CloseMenu();
        }
    }

    private int FindMnemonic(VirtualKey key)
    {
        // 문자 키(A~Z)만 대상.
        if (key < VirtualKey.A || key > VirtualKey.Z)
        {
            return -1;
        }
        char c = (char)key;   // VirtualKey.A~Z = 'A'~'Z'
        for (int i = 0; i < _mnemonics.Count; i++)
        {
            if (_mnemonics[i] == c)
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }
}
