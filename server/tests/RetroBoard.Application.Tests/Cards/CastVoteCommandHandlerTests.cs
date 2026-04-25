using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Commands.CastVote;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Cards;

public class CastVoteCommandHandlerTests
{
    private async Task<Guid> SeedCardAsync(TestSupport.FakeClock clock, Infrastructure.Persistence.BoardDbContext db)
    {
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var card = await new AddCardCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new AddCardCommand("retro", "x", "Alice", 0), default);
        return card.Id;
    }

    [Fact]
    public async Task First_vote_returns_voted_true_count_one()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cardId = await SeedCardAsync(clock, db);
        var pub = Substitute.For<IPublisher>();
        var result = await new CastVoteCommandHandler(db, pub, clock)
            .Handle(new CastVoteCommand("retro", cardId, "sess-1"), default);
        result.Voted.Should().BeTrue();
        result.Votes.Should().Be(1);
        await pub.Received(1).Publish(Arg.Any<VoteCastNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeat_vote_same_session_returns_voted_false_count_unchanged()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cardId = await SeedCardAsync(clock, db);
        var handler = new CastVoteCommandHandler(db, Substitute.For<IPublisher>(), clock);
        await handler.Handle(new CastVoteCommand("retro", cardId, "sess-1"), default);
        var second = await handler.Handle(new CastVoteCommand("retro", cardId, "sess-1"), default);
        second.Voted.Should().BeFalse();
        second.Votes.Should().Be(1);
    }

    [Fact]
    public async Task Throws_NotFound_when_card_missing()
    {
        var db = TestDb.NewInMemory();
        var act = () => new CastVoteCommandHandler(db, Substitute.For<IPublisher>(), new FakeClock(DateTimeOffset.UtcNow))
            .Handle(new CastVoteCommand("retro", Guid.NewGuid(), "s"), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
