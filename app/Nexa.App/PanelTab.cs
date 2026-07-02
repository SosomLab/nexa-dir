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

    // 탭별 가상화 목록 — 자체 코어 트리 핸들을 소유한다(성능 슬라이스 4-2).
    // 탭 전환 시 재-Open(재열거·재펼침) 없이 이 컬렉션의 ItemsSource를 그대로 재사용한다.
    // (VirtualTreeCollection은 Nexa.App 내부 타입 → internal 멤버로 노출.)
    internal readonly VirtualTreeCollection Items = new();

    /// <summary>현재 경로(<see cref="Current"/>)가 이미 열려(Open) 있는가. false면 다음 표시 때 로드.</summary>
    public bool Loaded;

    /// <summary>헤더 표시용 직접 자식 수(펼침 재적용 전) — 캐시 전환 시 헤더 복원.</summary>
    public int DirectChildCount;

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
