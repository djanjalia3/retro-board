using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Cards;

public class AddCardCommandHandlerTests
{
    [Fact]
    public async Task Adds_card_returns_dto_publishes_notification()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var board = await new CreateBoardCommandHandler(db, clock)
            .Handle(new CreateBoardCommand("Retro", null), default);
        var publisher = Substitute.For<IPublisher>();
        var handler = new AddCardCommandHandler(db, clock, publisher);

        var card = await handler.Handle(
            new AddCardCommand("retro", "Awesome", "Alice", 0), default);

        card.Text.Should().Be("Awesome");
        card.Author.Should().Be("Alice");
        card.ColumnIndex.Should().Be(0);
        card.Votes.Should().Be(0);
        await publisher.Received(1).Publish(
            Arg.Is<CardAddedNotification>(n => n.Slug == "retro" && n.Card.Id == card.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_NotFound_when_board_missing()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new AddCardCommandHandler(db, clock, Substitute.For<IPublisher>());

        var act = () => handler.Handle(new AddCardCommand("nope", "x", "Alice", 0), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_NotFound_when_column_index_out_of_range()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var handler = new AddCardCommandHandler(db, clock, Substitute.For<IPublisher>());

        var act = () => handler.Handle(new AddCardCommand("retro", "x", "Alice", 99), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
