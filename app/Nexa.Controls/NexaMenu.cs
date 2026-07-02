using System;
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

/// <summary>드롭다운 항목 하나. 체크형(토글) 지원. 후속: 아이콘·구분선·하위 메뉴.</summary>
public sealed class NexaMenuEntry
{
    /// <summary>항목 표시 텍스트.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>체크형(토글) 항목인지 — <c>true</c>면 체크 표시 칸을 두고 탭할 때마다 <see cref="IsChecked"/>가 토글된다.</summary>
    public bool IsCheckable { get; set; }

    /// <summary>현재 체크(켜짐) 상태. 체크형일 때만 의미. 호스트가 초기값을 설정하고, 탭 시 바가 토글한다.</summary>
    public bool IsChecked { get; set; }

    /// <summary>항목이 실행(탭)됐을 때 발생. 체크형이면 <see cref="IsChecked"/>가 토글된 <b>이후</b> 발생한다.</summary>
    public event EventHandler? Click;

    /// <summary>바가 탭을 처리한 뒤 호출(내부용).</summary>
    internal void RaiseClick() => Click?.Invoke(this, EventArgs.Empty);
}
