using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Nexa.Controls;

namespace Nexa.App;

/// <summary>
/// 패널 하나(좌 또는 우)의 <b>상태 + UI 참조</b>를 묶는다. <see cref="MainWindow"/>는 좌/우 <see cref="PanelView"/>
/// 2개(<c>_left</c>/<c>_right</c>)만 들고, 흩어져 있던 <c>bool left ? _leftX : _rightX</c> 분기를 <c>Panel(left).X</c>로
/// 통합한다(감사 B-2 — <c>bool left</c> 이중화 소거, C3 UI 얹기 전 이음새). 순수 로직 분리는 후속 ViewModels(B-1).
/// </summary>
internal sealed class PanelView
{
    /// <summary>좌측 패널 여부.</summary>
    public required bool IsLeft { get; init; }

    /// <summary>파일 목록/트리 그리드(<c>DirGrid</c>/<c>DirGrid2</c>).</summary>
    public required NexaFileGrid Grid { get; init; }

    /// <summary>경로/항목 수 헤더(<c>DirHeader</c>/<c>DirHeader2</c>).</summary>
    public required TextBlock Header { get; init; }

    /// <summary>계층 경로 바(<c>PathBarL</c>/<c>PathBarR</c>).</summary>
    public required NexaPathBar PathBar { get; init; }

    /// <summary>탭 바 스트립(<c>LeftTabs</c>/<c>RightTabs</c>).</summary>
    public required ItemsRepeater TabStrip { get; init; }

    /// <summary>이 패널의 탭 목록(탭 바 ItemsSource).</summary>
    public ObservableCollection<PanelTab> Tabs { get; } = new();

    /// <summary>현재 활성 탭(이 패널의 현재 뷰). 탭 전환 시 교체.</summary>
    public PanelTab Active { get; set; } = new();

    /// <summary>비동기 로드 재진입 가드 세대(로드 중 재이동 시 이전 결과 폐기, 감사 P1).</summary>
    public int LoadGen;

    /// <summary>활성 탭의 가상화 목록(코어 트리 가시행 스트림).</summary>
    public VirtualTreeCollection Items => Active.Items;
}
