using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Nexa.Controls;

/// <summary>컬럼 정렬 3상태(헤더 클릭 순환: 없음 → 오름 → 내림, docs/23 §4).</summary>
public enum ColumnSort
{
    None,
    Ascending,
    Descending,
}

/// <summary>
/// <see cref="NexaFileGrid"/> 컬럼 정의(도메인 비종속). 헤더/너비/키/정렬가능 여부를 안다.
/// 하나의 인스턴스를 헤더·본문·좌/우가 공유하면 <b>너비 리사이즈가 동시에 반영</b>된다(A3/A4).
/// <b>정렬 상태는 컬럼이 아니라 패널별 <see cref="HeaderCell"/>이 보유</b> → 좌/우 독립 정렬 표시(docs/23 §3-1).
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
                Raise(nameof(Width));
            }
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>헤더 정렬 클릭이 만든 정렬 서술자(컨트롤→호스트). 호스트가 <see cref="NexaGridColumn.Key"/>를 코어 키로 매핑.</summary>
public readonly record struct SortDescriptor(string Key, bool Descending, int Order);

/// <summary>
/// 헤더 셀 뷰모델(<b>패널별</b>). 공유 <see cref="NexaGridColumn"/>(너비·헤더·키)을 참조하되 <b>정렬 상태는
/// 이 셀만</b> 보유 → 좌/우 패널이 독립된 정렬 표시(▲/▼)를 가진다(docs/23 §3-1, per-panel). 너비는 공유
/// 컬럼에 바인딩하므로 리사이즈는 여전히 좌/우 동기.
/// </summary>
public sealed class HeaderCell : INotifyPropertyChanged
{
    public HeaderCell(NexaGridColumn column) => Column = column;

    /// <summary>공유 컬럼 정의(너비 동기·헤더·키·정렬가능).</summary>
    public NexaGridColumn Column { get; }

    private ColumnSort _sort = ColumnSort.None;

    /// <summary>이 패널에서의 정렬 방향(없음/오름/내림). 변경 시 <see cref="Glyph"/> 갱신.</summary>
    public ColumnSort Sort
    {
        get => _sort;
        set
        {
            if (_sort != value)
            {
                _sort = value;
                Raise(nameof(Sort));
                Raise(nameof(Glyph));
                Raise(nameof(GlyphVisibility));
            }
        }
    }

    /// <summary>다중 컬럼 정렬 순번(1차=1…, 없음=0). COL-3(Shift+헤더)에서 사용.</summary>
    public int Order { get; set; }

    private string _orderText = string.Empty;

    /// <summary>컬럼명 뒤에 표시할 정렬 순번 원문자(①②③…). 정렬 안 됐으면 빈 문자열.</summary>
    public string OrderText
    {
        get => _orderText;
        set
        {
            if (_orderText != value)
            {
                _orderText = value;
                Raise(nameof(OrderText));
                Raise(nameof(OrderVisibility));
            }
        }
    }

    /// <summary>컬럼명 <b>앞</b>에 표시할 정렬 화살표(▲ 오름 / ▼ 내림 / 없으면 빈 문자열).</summary>
    public string Glyph => _sort switch
    {
        ColumnSort.Ascending => "▲",
        ColumnSort.Descending => "▼",
        _ => string.Empty,
    };

    /// <summary>화살표 표시 여부(정렬 없으면 접어 간격 제거).</summary>
    public Visibility GlyphVisibility =>
        _sort == ColumnSort.None ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>순번 원문자 표시 여부(빈 문자열이면 접어 간격 제거).</summary>
    public Visibility OrderVisibility =>
        string.IsNullOrEmpty(_orderText) ? Visibility.Collapsed : Visibility.Visible;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
