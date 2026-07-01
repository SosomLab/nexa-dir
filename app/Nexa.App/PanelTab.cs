using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Nexa.App;

/// <summary>
/// 패널의 탭 하나. 탭마다 **자체 이동 기록**(뒤로/앞으로 + 현재 경로)과 **펼침 상태**(경로 집합)를 가진다.
/// 활성 탭이 그 패널의 현재 뷰. Title/IsActive는 탭 바 UI에 바인딩(INotifyPropertyChanged).
/// </summary>
public sealed class PanelTab : INotifyPropertyChanged
{
    // 이동 기록(F13) — 탭별.
    public string Current = string.Empty;
    public readonly Stack<string> Back = new();
    public readonly Stack<string> Fwd = new();

    // 펼침 상태 유지(F18) — 탭별(경로, 대소문자 무시).
    public readonly HashSet<string> Expanded = new(StringComparer.OrdinalIgnoreCase);

    private string _title = string.Empty;
    /// <summary>탭에 표시할 이름(현재 폴더명).</summary>
    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPc(nameof(Title)); } }
    }

    private bool _isActive;
    /// <summary>활성 탭 여부 → 배경/상단 하이라이트 줄에 반영.</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) { return; }
            _isActive = value;
            OnPc(nameof(IsActive));
            OnPc(nameof(TabBackground));
            OnPc(nameof(AccentVisibility));
        }
    }

    /// <summary>탭 배경(활성=진한 파랑, 비활성=옅은 흰색).</summary>
    public Brush TabBackground => new SolidColorBrush(
        _isActive ? Color.FromArgb(0x66, 0x40, 0xA0, 0xFF) : Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));

    /// <summary>활성 탭 상단 하이라이트 줄 표시 여부.</summary>
    public Visibility AccentVisibility => _isActive ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPc(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
