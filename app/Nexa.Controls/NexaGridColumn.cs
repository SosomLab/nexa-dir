using System.ComponentModel;

namespace Nexa.Controls;

/// <summary>
/// <see cref="NexaFileGrid"/> 컬럼 정의(도메인 비종속). 헤더/너비/키/정렬가능 여부만 안다.
/// 하나의 인스턴스를 헤더·본문·좌/우가 공유하면 <b>너비 리사이즈가 동시에 반영</b>된다(A3/A4).
/// 정렬 상태는 컬럼이 아니라 <b>패널(NexaFileGrid)별</b>로 관리한다(정렬은 좌/우 독립, docs/23 §3-1·§4).
/// </summary>
public sealed class NexaGridColumn : INotifyPropertyChanged
{
    /// <summary>헤더 표시 텍스트.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>컬럼 식별 키(정렬·값 조회용).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>헤더 클릭 정렬 허용 여부.</summary>
    public bool Sortable { get; set; } = true;

    private double _width = 120;

    /// <summary>컬럼 픽셀 너비. 변경 시 이 컬럼을 참조하는 모든 헤더/셀이 갱신된다(리사이즈 동기화).</summary>
    public double Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                _width = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
