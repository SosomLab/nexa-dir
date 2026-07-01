using System.Collections.Generic;
using Microsoft.UI.Xaml.Markup;

namespace Nexa.Controls;

/// <summary>
/// <see cref="NexaMenuBar"/>의 최상위 메뉴 하나(예: "파일"). 헤더 + 드롭다운 항목 목록.
/// 도메인 비종속 — 실제 명령 연결(Command/Click)은 후속. 지금은 표시/구조용.
/// </summary>
[ContentProperty(Name = nameof(Items))]
public sealed class NexaMenu
{
    /// <summary>메뉴 헤더 표시 텍스트(예: "파일(F)").</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>드롭다운 항목(비어 있으면 드롭다운 없음).</summary>
    public IList<NexaMenuEntry> Items { get; } = new List<NexaMenuEntry>();
}

/// <summary>드롭다운 항목 하나. 후속: Command·아이콘·구분선·하위 메뉴.</summary>
public sealed class NexaMenuEntry
{
    /// <summary>항목 표시 텍스트.</summary>
    public string Text { get; set; } = string.Empty;
}
