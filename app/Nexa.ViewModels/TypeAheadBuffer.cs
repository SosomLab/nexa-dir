namespace Nexa.ViewModels;

/// <summary>
/// 타입어헤드 입력 버퍼(UI 비종속 순수 로직, docs/32 §8 2단계). 문자 누적·타임아웃·반복키 cycle·Backspace를
/// 관리하고, 검색할 <b>접두사</b>와 <b>검색 시작 규칙</b>을 산출한다. <b>시각(now)은 주입</b> → 맥/CI 단위 테스트.
///
/// <para>규약(docs/32 §6):
/// <list type="bullet">
/// <item>타임아웃(기본 1000ms) 지나면 버퍼 리셋 후 새 접두사.</item>
/// <item><b>반복키 cycle</b>: 버퍼가 한 글자의 반복이고 같은 글자를 또 치면 누적이 아니라 그 글자 <b>다음 매치로 cycle</b>(단일 글자 유지).</item>
/// <item><b>확장(refine)</b>: 다른 글자를 이어치면 접두사 누적 → 검색을 <b>현재 캐럿 포함</b>(현재 항목이 여전히 매치면 유지).</item>
/// <item>새 시작·반복은 <b>캐럿 다음</b>부터 검색(이동/cycle).</item>
/// </list></para>
/// </summary>
public sealed class TypeAheadBuffer
{
    private readonly long _timeoutMs;
    private string _buffer = string.Empty;
    private long _lastInputMs;
    private bool _hasInput;

    public TypeAheadBuffer(long timeoutMs) => _timeoutMs = timeoutMs;

    /// <summary>현재 검색 접두사(빈 문자열이면 무동작).</summary>
    public string Prefix => _buffer;

    /// <summary>버퍼가 비었는가.</summary>
    public bool IsEmpty => _buffer.Length == 0;

    /// <summary>
    /// 마지막 입력이 <b>확장(refine)</b>이었는가 — true면 검색을 현재 캐럿 <b>포함</b>(유지 우선),
    /// false(새 시작·반복 cycle)면 캐럿 <b>다음</b>부터(이동). 호스트가 검색 시작 인덱스 계산에 사용.
    /// </summary>
    public bool IsExtend { get; private set; }

    /// <summary>문자 입력 처리(수정키·Space 제외는 호출측이 판단, IME는 확정문자만 전달). 반환: 검색 접두사.</summary>
    public string Push(char c, long nowMs)
    {
        ResetIfExpired(nowMs);
        Touch(nowMs);

        bool wasEmpty = _buffer.Length == 0;
        if (!wasEmpty && AllSame(_buffer) && _buffer[0] == c)
        {
            _buffer = c.ToString();   // 반복키 → 단일 글자 유지(cycle)
            IsExtend = false;
        }
        else if (wasEmpty)
        {
            _buffer = c.ToString();   // 새 시작
            IsExtend = false;
        }
        else
        {
            _buffer += c;             // 확장(refine)
            IsExtend = true;
        }
        return _buffer;
    }

    /// <summary>Backspace — 마지막 글자 제거(접두사 축소·현재 포함 재평가). 반환: 남은 접두사.</summary>
    public string Backspace(long nowMs)
    {
        ResetIfExpired(nowMs);
        Touch(nowMs);
        if (_buffer.Length > 0)
        {
            _buffer = _buffer[..^1];
        }
        IsExtend = true;   // 축소도 현재 포함 재평가(유지 우선)
        return _buffer;
    }

    /// <summary>버퍼가 타임아웃을 지났는가(호스트가 표시 소거·리셋 판단용).</summary>
    public bool Expired(long nowMs) => _hasInput && nowMs - _lastInputMs > _timeoutMs;

    /// <summary>버퍼·상태 초기화(포커스 상실·명시적 취소 등).</summary>
    public void Reset()
    {
        _buffer = string.Empty;
        _hasInput = false;
        IsExtend = false;
    }

    private void ResetIfExpired(long nowMs)
    {
        if (_hasInput && nowMs - _lastInputMs > _timeoutMs)
        {
            _buffer = string.Empty;
        }
    }

    private void Touch(long nowMs)
    {
        _lastInputMs = nowMs;
        _hasInput = true;
    }

    private static bool AllSame(string s)
    {
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] != s[0])
            {
                return false;
            }
        }
        return s.Length > 0;
    }
}
