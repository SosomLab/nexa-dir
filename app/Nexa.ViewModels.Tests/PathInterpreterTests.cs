using System;
using Nexa.ViewModels;
using Xunit;

namespace Nexa.ViewModels.Tests;

public sealed class PathInterpreterTests
{
    // 테스트는 고유 이름의 환경변수를 직접 설정해 플랫폼(맥/Win) 무관하게 검증.
    private const string Var = "NEXA_TEST_ENV_DIR";
    private const string Value = @"C:\Nexa\Test Dir";

    public PathInterpreterTests() => Environment.SetEnvironmentVariable(Var, Value);

    [Fact]
    public void Cmd_percent_var_is_expanded()
    {
        Assert.Equal(Value + @"\sub", PathInterpreter.Expand($"%{Var}%\\sub"));
    }

    [Fact]
    public void Powershell_env_var_is_expanded()
    {
        Assert.Equal(Value + @"\sub", PathInterpreter.Expand($"$env:{Var}\\sub"));
    }

    [Fact]
    public void Powershell_braced_env_var_is_expanded()
    {
        Assert.Equal(Value, PathInterpreter.Expand($"${{env:{Var}}}"));
    }

    [Fact]
    public void Surrounding_quotes_and_whitespace_are_stripped()
    {
        Assert.Equal(Value, PathInterpreter.Expand($"  \"%{Var}%\"  "));
    }

    [Theory]
    [InlineData(@"C:\Users\me")]              // 평범한 경로는 그대로
    [InlineData("/home/user/docs")]
    public void Plain_path_is_unchanged(string path) =>
        Assert.Equal(path, PathInterpreter.Expand(path));

    [Fact]
    public void Undefined_var_is_left_verbatim()
    {
        // 미정의 변수는 원문 유지(→ 존재 검사에서 "경로 없음"으로 자연 실패).
        Assert.Equal(@"$env:NEXA_NOPE_XYZ\a", PathInterpreter.Expand(@"$env:NEXA_NOPE_XYZ\a"));
        Assert.Equal("%NEXA_NOPE_XYZ%", PathInterpreter.Expand("%NEXA_NOPE_XYZ%"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_is_safe(string? input) =>
        Assert.Equal(input ?? string.Empty, PathInterpreter.Expand(input));
}
