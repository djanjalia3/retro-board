using FluentAssertions;
using RetroBoard.Domain.Common;
using Xunit;

namespace RetroBoard.Domain.Tests.Common;

public class ParticipantKeyFactoryTests
{
    [Theory]
    [InlineData("Alice", "alice")]
    [InlineData("Bob Smith", "bob-smith")]
    public void Create_returns_slug_when_input_slugifiable(string input, string expected)
    {
        ParticipantKeyFactory.Create(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("!!!", "anon-3")]
    [InlineData("", "anon-0")]
    public void Create_falls_back_to_anon_with_length_when_slug_empty(string input, string expected)
    {
        ParticipantKeyFactory.Create(input).Should().Be(expected);
    }
}
