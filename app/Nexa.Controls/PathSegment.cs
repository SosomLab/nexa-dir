namespace Nexa.Controls;

/// <summary>경로 바의 세그먼트 1개(브레드크럼). <see cref="NexaPathBar"/>가 경로를 분해해 생성.</summary>
public sealed class PathSegment
{
    /// <summary>앞 구분자(첫 세그먼트는 빈 문자열, 이후 "\\").</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>표시 라벨(폴더명 또는 드라이브 "C:").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>이 세그먼트를 클릭했을 때 이동할 전체 경로(드라이브는 "C:\\").</summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>현재(마지막) 세그먼트 여부 — true면 클릭/hover 없음(이미 그 위치).</summary>
    public bool IsCurrent { get; set; }
}
