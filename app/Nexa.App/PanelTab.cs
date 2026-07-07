using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Nexa.ViewModels;

namespace Nexa.App;

/// <summary>
/// 패널의 탭 하나. 탭마다 **자체 이동 기록**(뒤로/앞으로 + 현재 경로)과 **펼침 상태**(경로 집합)를 가진다.
/// 활성 탭이 그 패널의 현재 뷰. Title/IsActive는 탭 바 UI에 바인딩(INotifyPropertyChanged).
/// </summary>
public sealed class PanelTab : INotifyPropertyChanged
{
    // 이동 기록(F13) — 탭별. 순수 로직은 Nexa.ViewModels.NavigationHistory로 분리(감사 B-1, 테스트 대상).
    public readonly NavigationHistory Nav = new();

    /// <summary>현재 경로(이동 기록의 현재 위치). 읽기 전용 패스스루 — 변경은 <see cref="Nav"/> 메서드로.</summary>
    public string Current => Nav.Current;

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

    /// <summary>탭 배경(활성=accent 알파, 비활성=중립 회색 알파 — 라이트/다크 공용, docs/39).</summary>
    public Brush TabBackground => new SolidColorBrush(
        _isActive ? Color.FromArgb(0x4D, 0x3D, 0x8B, 0xFF) : Color.FromArgb(0x14, 0x80, 0x80, 0x80));

    /// <summary>활성 탭 상단 하이라이트 줄 표시 여부.</summary>
    public Visibility AccentVisibility => _isActive ? Visibility.Visible : Visibility.Collapsed;

    private bool _isLocked;
    /// <summary>탭 잠금 — 닫기 동작(탭 닫기·모두 닫기·더블클릭 닫기)에서 제외(TAB-MENU). 열쇠 아이콘 표시.</summary>
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value) { return; }
            _isLocked = value;
            OnPc(nameof(IsLocked));
            NotifyBadge();
        }
    }

    private bool _isPinned;
    /// <summary>탭 고정 — 이름 앞 핀 아이콘 표시 + 핀 그룹(맨 앞)으로 이동(TAB-MENU).</summary>
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value) { return; }
            _isPinned = value;
            OnPc(nameof(IsPinned));
            NotifyBadge();
        }
    }

    // 탭 상태 아이콘: 핀=폴더명 앞(왼쪽 끝) · 잠금(열쇠)=탭 오른쪽 끝. 동시 설정이면 양 끝에 각각 표시.
    /// <summary>핀 아이콘(폴더명 앞) 표시 여부.</summary>
    public Visibility PinVisibility => _isPinned ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>잠금(열쇠) 아이콘(탭 오른쪽 끝) 표시 여부.</summary>
    public Visibility LockVisibility => _isLocked ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyBadge()
    {
        OnPc(nameof(PinVisibility));
        OnPc(nameof(LockVisibility));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPc(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
