using FluentAssertions;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Boards;

public class CreateBoardCommandHandlerTests
{
    [Fact]
    public async Task Creates_board_with_default_columns_when_none_supplied()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var handler = new CreateBoardCommandHandler(db, clock);

        var dto = await handler.Handle(new CreateBoardCommand("Sprint 12 Retro", null), default);

        dto.Slug.Should().Be("sprint-12-retro");
        dto.Name.Should().Be("Sprint 12 Retro");
        dto.Columns.Select(c => c.Title).Should().Equal(
            "What went well", "What didn't go well", "Shoutouts", "Action items");
        dto.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Throws_conflict_when_slug_exists()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new CreateBoardCommandHandler(db, clock);
        await handler.Handle(new CreateBoardCommand("Retro 1", null), default);

        var act = () => handler.Handle(new CreateBoardCommand("Retro 1", null), default);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Uses_supplied_columns_when_non_empty()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new CreateBoardCommandHandler(db, clock);

        var dto = await handler.Handle(new CreateBoardCommand("Custom", new[] { "A", "B" }), default);

        dto.Columns.Select(c => c.Title).Should().Equal("A", "B");
        dto.Columns.Select(c => c.Position).Should().Equal(0, 1);
    }
}
