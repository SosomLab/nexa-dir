using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

public sealed class NavigationHistoryTests
{
    [Fact]
    public void Initial_state_is_empty()
    {
        var h = new NavigationHistory();
        Assert.Equal(string.Empty, h.Current);
        Assert.False(h.CanGoBack);
        Assert.False(h.CanGoForward);
    }

    [Fact]
    public void First_navigate_sets_current_without_stacking()
    {
        var h = new NavigationHistory();
        h.NavigateTo(@"C:\a", record: true);   // 현재가 비어있으면 뒤로 스택에 안 쌓임
        Assert.Equal(@"C:\a", h.Current);
        Assert.False(h.CanGoBack);
    }

    [Fact]
    public void Recorded_navigate_pushes_back_and_clears_forward()
    {
        var h = new NavigationHistory();
        h.NavigateTo(@"C:\a", record: true);
        h.NavigateTo(@"C:\b", record: true);
        Assert.True(h.CanGoBack);
        Assert.False(h.CanGoForward);
        Assert.Equal(@"C:\b", h.Current);
    }

    [Fact]
    public void Back_then_forward_round_trips()
    {
        var h = new NavigationHistory();
        h.NavigateTo(@"C:\a", record: true);
        h.NavigateTo(@"C:\b", record: true);

        Assert.Equal(@"C:\a", h.GoBack());
        Assert.Equal(@"C:\a", h.Current);
        Assert.True(h.CanGoForward);

        Assert.Equal(@"C:\b", h.GoForward());
        Assert.Equal(@"C:\b", h.Current);
        Assert.False(h.CanGoForward);
    }

    [Fact]
    public void Back_at_start_returns_null_and_keeps_current()
    {
        var h = new NavigationHistory();
        h.NavigateTo(@"C:\a", record: true);
        Assert.Null(h.GoBack());
        Assert.Equal(@"C:\a", h.Current);
    }

    [Fact]
    public void New_branch_after_back_clears_forward()
    {
        var h = new NavigationHistory();
        h.NavigateTo(@"C:\a", record: true);
        h.NavigateTo(@"C:\b", record: true);
        h.GoBack();                             // 현재=a, 앞으로=[b]
        h.NavigateTo(@"C:\c", record: true);    // 새 분기 → 앞으로 비움
        Assert.False(h.CanGoForward);
        Assert.Equal(@"C:\c", h.Current);
        Assert.Equal(@"C:\a", h.GoBack());
    }
}
