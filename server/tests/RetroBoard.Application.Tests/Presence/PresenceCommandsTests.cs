using FluentAssertions;
using MediatR;
using NSubstitute;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Commands.LeaveBoard;
using RetroBoard.Application.Presence.Commands.RefreshPresence;
using RetroBoard.Application.Presence.Commands.SweepStalePresence;
using RetroBoard.Application.Presence.Notifications;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Presence;

public class PresenceCommandsTests
{
    [Fact]
    public async Task Join_creates_participant_and_connection_publishes_presence()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var pub = Substitute.For<IPublisher>();

        var result = await new JoinBoardCommandHandler(db, clock, pub)
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);

        result.Participants.Should().HaveCount(1);
        result.Participants[0].DisplayName.Should().Be("Alice");
        result.Participants[0].ConnectionCount.Should().Be(1);
        await pub.Received(1).Publish(Arg.Any<PresenceChangedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leave_removes_connection_and_participant_when_last()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        var pub = Substitute.For<IPublisher>();
        await new JoinBoardCommandHandler(db, clock, pub)
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);

        await new LeaveBoardCommandHandler(db, pub)
            .Handle(new LeaveBoardCommand("retro", "conn-1"), default);

        db.Participants.Should().BeEmpty();
        db.ParticipantConnections.Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_updates_last_seen_and_connected_at()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        await new JoinBoardCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);
        clock.Advance(TimeSpan.FromMinutes(1));

        await new RefreshPresenceCommandHandler(db, clock)
            .Handle(new RefreshPresenceCommand("conn-1"), default);

        var conn = db.ParticipantConnections.Single();
        conn.ConnectedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task Sweep_removes_stale_connections_and_participants()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        await new CreateBoardCommandHandler(db, clock).Handle(new CreateBoardCommand("Retro", null), default);
        await new JoinBoardCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new JoinBoardCommand("retro", "conn-1", "sess-1", "Alice"), default);
        clock.Advance(TimeSpan.FromMinutes(10));

        await new SweepStalePresenceCommandHandler(db, clock, Substitute.For<IPublisher>())
            .Handle(new SweepStalePresenceCommand(TimeSpan.FromMinutes(5)), default);

        db.Participants.Should().BeEmpty();
        db.ParticipantConnections.Should().BeEmpty();
    }
}
