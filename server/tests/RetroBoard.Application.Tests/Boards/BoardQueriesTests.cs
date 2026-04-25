using FluentAssertions;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Boards.Queries.BoardExists;
using RetroBoard.Application.Boards.Queries.GetBoard;
using RetroBoard.Application.Boards.Queries.ListBoards;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Boards;

public class BoardQueriesTests
{
    [Fact]
    public async Task GetBoard_returns_null_when_missing()
    {
        var db = TestDb.NewInMemory();
        var dto = await new GetBoardQueryHandler(db).Handle(new GetBoardQuery("no-such"), default);
        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetBoard_returns_board_with_columns_and_cards()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);

        var dto = await new GetBoardQueryHandler(db).Handle(new GetBoardQuery("retro"), default);

        dto.Should().NotBeNull();
        dto!.Slug.Should().Be("retro");
        dto.Columns.Should().HaveCount(4);
        dto.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBoards_returns_summaries_newest_first()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var create = new CreateBoardCommandHandler(db, clock);
        await create.Handle(new CreateBoardCommand("First", null), default);
        clock.Advance(TimeSpan.FromHours(1));
        await create.Handle(new CreateBoardCommand("Second", null), default);

        var list = await new ListBoardsQueryHandler(db).Handle(new ListBoardsQuery(), default);
        list.Select(x => x.Slug).Should().Equal("second", "first");
    }

    [Fact]
    public async Task BoardExists_returns_true_then_false()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);

        (await new BoardExistsQueryHandler(db).Handle(new BoardExistsQuery("retro"), default)).Should().BeTrue();
        (await new BoardExistsQueryHandler(db).Handle(new BoardExistsQuery("nope"), default)).Should().BeFalse();
    }
}
