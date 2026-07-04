using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

public sealed class TypeAheadBufferTests
{
    [Fact]
    public void Accumulates_different_chars_within_timeout()
    {
        var b = new TypeAheadBuffer(1000);
        Assert.Equal("a", b.Push('a', 0));
        Assert.False(b.IsExtend);              // 새 시작
        Assert.Equal("ap", b.Push('p', 100));  // 확장
        Assert.True(b.IsExtend);
        Assert.Equal("app", b.Push('p', 200)); // "ap"+"p"는 반복 아님(전체가 같은 글자 아님) → 확장
        Assert.True(b.IsExtend);
    }

    [Fact]
    public void Repeat_same_char_cycles_keeps_single_char()
    {
        var b = new TypeAheadBuffer(1000);
        Assert.Equal("s", b.Push('s', 0));
        Assert.Equal("s", b.Push('s', 100));   // 반복 → 단일 유지(cycle)
        Assert.False(b.IsExtend);              // cycle은 캐럿 다음부터
        Assert.Equal("s", b.Push('s', 200));
        Assert.False(b.IsExtend);
    }

    [Fact]
    public void Timeout_resets_to_new_prefix()
    {
        var b = new TypeAheadBuffer(1000);
        Assert.Equal("a", b.Push('a', 0));
        Assert.Equal("b", b.Push('b', 2000)); // 타임아웃(>1000) → "ab" 아니라 "b"
        Assert.False(b.IsExtend);
    }

    [Fact]
    public void Within_timeout_boundary_accumulates()
    {
        var b = new TypeAheadBuffer(1000);
        b.Push('a', 0);
        Assert.Equal("ab", b.Push('b', 1000)); // 정확히 1000ms(초과 아님) → 누적
    }

    [Fact]
    public void Backspace_shrinks_prefix()
    {
        var b = new TypeAheadBuffer(1000);
        b.Push('a', 0);
        b.Push('b', 100);
        Assert.Equal("a", b.Backspace(200));
        Assert.True(b.IsExtend);               // 축소도 현재 포함 재평가
        Assert.Equal("", b.Backspace(300));
    }

    [Fact]
    public void Expired_reports_timeout_and_reset_clears()
    {
        var b = new TypeAheadBuffer(1000);
        b.Push('a', 0);
        Assert.False(b.Expired(500));
        Assert.True(b.Expired(2000));
        b.Reset();
        Assert.True(b.IsEmpty);
        Assert.False(b.Expired(9999)); // 리셋 후 입력 없음 → 만료 아님
    }

    [Fact]
    public void Different_then_same_is_accumulate_not_cycle()
    {
        var b = new TypeAheadBuffer(1000);
        b.Push('a', 0);
        b.Push('b', 100);                      // "ab"
        Assert.Equal("abb", b.Push('b', 200)); // "ab"는 all-same 아님 → 확장
        Assert.True(b.IsExtend);
    }
}
