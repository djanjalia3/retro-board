using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Notifications;

namespace RetroBoard.Application.Presence.Commands.SweepStalePresence;

public class SweepStalePresenceCommandHandler(IBoardDbContext db, IClock clock, IPublisher publisher)
    : IRequestHandler<SweepStalePresenceCommand, Unit>
{
    public async Task<Unit> Handle(SweepStalePresenceCommand cmd, CancellationToken ct)
    {
        var threshold = clock.UtcNow - cmd.StaleAfter;
        var stale = await db.ParticipantConnections
            .Where(c => c.ConnectedAt < threshold)
            .ToListAsync(ct);
        if (stale.Count == 0) return Unit.Value;

        var affectedParticipantIds = stale.Select(c => c.ParticipantId).Distinct().ToList();
        db.ParticipantConnections.RemoveRange(stale);
        await db.SaveChangesAsync(ct);

        var emptyParticipants = await db.Participants
            .Where(p => affectedParticipantIds.Contains(p.Id) && !p.Connections.Any())
            .ToListAsync(ct);
        var affectedBoards = emptyParticipants.Select(p => p.BoardId).Distinct().ToList();
        if (!affectedBoards.Any())
            affectedBoards = await db.Participants
                .Where(p => affectedParticipantIds.Contains(p.Id))
                .Select(p => p.BoardId)
                .Distinct()
                .ToListAsync(ct);

        db.Participants.RemoveRange(emptyParticipants);
        await db.SaveChangesAsync(ct);

        foreach (var boardId in affectedBoards)
        {
            var slug = await db.Boards.Where(b => b.Id == boardId).Select(b => b.Slug).FirstOrDefaultAsync(ct);
            if (slug is null) continue;
            var participants = await JoinBoardCommandHandler.LoadParticipantsAsync(db, boardId, ct);
            await publisher.Publish(new PresenceChangedNotification(slug, participants), ct);
        }
        return Unit.Value;
    }
}
