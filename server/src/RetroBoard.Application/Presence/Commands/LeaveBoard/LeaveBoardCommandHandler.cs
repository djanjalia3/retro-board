using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Notifications;

namespace RetroBoard.Application.Presence.Commands.LeaveBoard;

public class LeaveBoardCommandHandler(IBoardDbContext db, IPublisher publisher)
    : IRequestHandler<LeaveBoardCommand, Unit>
{
    public async Task<Unit> Handle(LeaveBoardCommand cmd, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Slug == cmd.Slug, ct);
        if (board is null) return Unit.Value;

        var connections = await db.ParticipantConnections
            .Where(c => c.ConnectionId == cmd.ConnectionId &&
                db.Participants.Any(p => p.Id == c.ParticipantId && p.BoardId == board.Id))
            .ToListAsync(ct);
        if (connections.Count == 0) return Unit.Value;

        db.ParticipantConnections.RemoveRange(connections);
        await db.SaveChangesAsync(ct);

        var emptyParticipants = await db.Participants
            .Where(p => p.BoardId == board.Id && !p.Connections.Any())
            .ToListAsync(ct);
        db.Participants.RemoveRange(emptyParticipants);
        await db.SaveChangesAsync(ct);

        var participants = await JoinBoardCommandHandler.LoadParticipantsAsync(db, board.Id, ct);
        await publisher.Publish(new PresenceChangedNotification(cmd.Slug, participants), ct);
        return Unit.Value;
    }
}
