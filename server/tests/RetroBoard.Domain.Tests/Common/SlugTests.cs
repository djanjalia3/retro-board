using FluentAssertions;
using RetroBoard.Domain.Common;
using Xunit;

namespace RetroBoard.Domain.Tests.Common;

public class SlugTests
{
    [Theory]
    [InlineData("Sprint 12 Retro", "sprint-12-retro")]
    [InlineData("  Hello   World  ", "hello-world")]
    [InlineData("Q1!! Review??", "q1-review")]
    [InlineData("--leading-and-trailing--", "leading-and-trailing")]
    [InlineData("MIXED Case 123", "mixed-case-123")]
    public void Create_returns_lowercase_hyphenated_slug(string input, string expected)
    {
        Slug.Create(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Create_throws_when_input_yields_empty_slug(string input)
    {
        var act = () => Slug.Create(input);
        act.Should().Throw<ArgumentException>().WithMessage("*invalid board name*");
    }
}
