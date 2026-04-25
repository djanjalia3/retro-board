using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Commands.DeleteCard;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Cards;

public class DeleteCardCommandHandlerTests
{
    [Fact]
    public async Task Deletes_card_publishes_notification()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var publisher = Substitute.For<IPublisher>();
        var card = await new AddCardCommandHandler(db, clock, publisher)
            .Handle(new AddCardCommand("retro", "x", "Alice", 0), default);

        await new DeleteCardCommandHandler(db, publisher)
            .Handle(new DeleteCardCommand("retro", card.Id), default);

        db.Cards.Should().BeEmpty();
        await publisher.Received(1).Publish(
            Arg.Is<CardDeletedNotification>(n => n.Slug == "retro" && n.CardId == card.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_NotFound_when_card_missing()
    {
        var db = TestDb.NewInMemory();
        var act = () => new DeleteCardCommandHandler(db, Substitute.For<IPublisher>())
            .Handle(new DeleteCardCommand("retro", Guid.NewGuid()), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
