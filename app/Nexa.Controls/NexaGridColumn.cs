namespace Nexa.Controls;

/// <summary>
/// <see cref="NexaFileGrid"/> 컬럼 정의(도메인 비종속). 헤더/너비/키만 안다 — 셀 값은
/// 행 템플릿(도메인 주입)이 렌더한다. 후속: 정렬 가능·표시 토글·리사이즈(A3)·`ICellValueProvider`.
/// </summary>
public sealed class NexaGridColumn
{
    /// <summary>헤더 표시 텍스트.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>컬럼 픽셀 너비(초안: 고정). A3에서 리사이즈로 확장.</summary>
    public double Width { get; set; } = 120;

    /// <summary>컬럼 식별 키(정렬·동기화·값 조회용).</summary>
    public string Key { get; set; } = string.Empty;
}
