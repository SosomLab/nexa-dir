using System.ComponentModel;

namespace Nexa.Controls;

/// <summary>컬럼 정렬 3상태(헤더 클릭 순환: 없음 → 오름 → 내림, docs/23 §4).</summary>
public enum ColumnSort
{
    None,
    Ascending,
    Descending,
}

/// <summary>
/// <see cref="NexaFileGrid"/> 컬럼 정의(도메인 비종속). 헤더/너비/키/정렬가능 여부·정렬 상태를 안다.
/// 하나의 인스턴스를 헤더·본문·좌/우가 공유하면 <b>너비 리사이즈가 동시에 반영</b>된다(A3/A4).
/// COL-2c(MVP): 컬럼이 좌/우 공유이므로 <b>정렬 표시(▲/▼)도 공유</b> — 헤더 클릭은 양쪽 패널을 동일
/// 정렬로 적용한다. 패널별 독립 정렬(docs/23 §3-1)은 per-panel ColumnLayout(§6-3) 도입 후속.
/// </summary>
public sealed class NexaGridColumn : INotifyPropertyChanged
{
    /// <summary>헤더 표시 텍스트.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>컬럼 식별 키(정렬·값 조회용).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>헤더 클릭 정렬 허용 여부.</summary>
    public bool Sortable { get; set; } = true;

    private ColumnSort _sortDirection = ColumnSort.None;

    /// <summary>현재 정렬 방향(없음/오름/내림). 변경 시 헤더 글리프(<see cref="SortGlyph"/>)가 갱신된다.</summary>
    public ColumnSort SortDirection
    {
        get => _sortDirection;
        set
        {
            if (_sortDirection != value)
            {
                _sortDirection = value;
                Raise(nameof(SortDirection));
                Raise(nameof(SortGlyph));
            }
        }
    }

    private int _sortOrder;

    /// <summary>다중 컬럼 정렬 순번(1차=1…, 없음=0). COL-3(Alt+헤더)에서 사용.</summary>
    public int SortOrder
    {
        get => _sortOrder;
        set
        {
            if (_sortOrder != value)
            {
                _sortOrder = value;
                Raise(nameof(SortOrder));
            }
        }
    }

    /// <summary>헤더에 표시할 정렬 글리프(▲ 오름 / ▼ 내림 / 없으면 빈 문자열).</summary>
    public string SortGlyph => _sortDirection switch
    {
        ColumnSort.Ascending => "▲",   // ▲
        ColumnSort.Descending => "▼",  // ▼
        _ => string.Empty,
    };

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
