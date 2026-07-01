using System.ComponentModel;

namespace Nexa.Controls;

/// <summary>
/// <see cref="NexaFileGrid"/> 컬럼 정의(도메인 비종속). 헤더/너비/키만 안다 — 셀 값은
/// 행 템플릿(도메인 주입)이 렌더한다. <see cref="Width"/>는 변경 알림(INPC)이므로 하나의
/// 컬럼 인스턴스를 **헤더·본문·좌/우 패널이 공유**하면 리사이즈가 동시에 반영된다(A3·A4).
/// </summary>
public sealed class NexaGridColumn : INotifyPropertyChanged
{
    /// <summary>헤더 표시 텍스트.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>컬럼 식별 키(정렬·동기화·값 조회용).</summary>
    public string Key { get; set; } = string.Empty;

    private double _width = 120;

    /// <summary>컬럼 픽셀 너비. 변경 시 이 컬럼을 참조하는 모든 헤더/셀이 갱신된다.</summary>
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
