namespace Nexa.ViewModels;

/// <summary>
/// 탭 하나의 <b>이동 기록</b>(뒤로/앞으로 스택 + 현재 경로) — UI 비종속 순수 로직(감사 B-1, F13).
/// 브라우저식 의미: 새 위치로 이동하면 현재를 뒤로 스택에 쌓고 앞으로 스택을 비운다. 뒤로/앞으로는
/// 두 스택 사이로 현재를 옮긴다. 맥/Windows 공통 단위 테스트 대상.
/// </summary>
public sealed class NavigationHistory
{
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _fwd = new();

    /// <summary>현재 경로(초기값 빈 문자열).</summary>
    public string Current { get; private set; } = string.Empty;

    /// <summary>뒤로 이동 가능 여부.</summary>
    public bool CanGoBack => _back.Count > 0;

    /// <summary>앞으로 이동 가능 여부.</summary>
    public bool CanGoForward => _fwd.Count > 0;

    /// <summary>
    /// <paramref name="path"/>로 이동한다. <paramref name="record"/>가 true고 현재 경로가 비어있지 않으면
    /// 현재를 뒤로 스택에 쌓고 앞으로 스택을 비운다(새 분기). false면 기록 없이 현재만 바꾼다(뒤로/앞으로 내부용).
    /// </summary>
    public void NavigateTo(string path, bool record)
    {
        if (record && !string.IsNullOrEmpty(Current))
        {
            _back.Push(Current);
            _fwd.Clear();
        }
        Current = path;
    }

    /// <summary>뒤로 이동하고 새 현재 경로를 반환한다(불가하면 <c>null</c>, 현재 유지).</summary>
    public string? GoBack()
    {
        if (_back.Count == 0)
        {
            return null;
        }
        _fwd.Push(Current);
        Current = _back.Pop();
        return Current;
    }

    /// <summary>앞으로 이동하고 새 현재 경로를 반환한다(불가하면 <c>null</c>, 현재 유지).</summary>
    public string? GoForward()
    {
        if (_fwd.Count == 0)
        {
            return null;
        }
        _back.Push(Current);
        Current = _fwd.Pop();
        return Current;
    }
}
