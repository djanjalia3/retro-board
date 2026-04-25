using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Application.Presence.Commands.RefreshPresence;

public class RefreshPresenceCommandHandler(IBoardDbContext db, IClock clock)
    : IRequestHandler<RefreshPresenceCommand, Unit>
{
    public async Task<Unit> Handle(RefreshPresenceCommand cmd, CancellationToken ct)
    {
        var conn = await db.ParticipantConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == cmd.ConnectionId, ct);
        if (conn is null) return Unit.Value;
        conn.ConnectedAt = clock.UtcNow;

        var participant = await db.Participants.FirstOrDefaultAsync(p => p.Id == conn.ParticipantId, ct);
        if (participant is not null) participant.LastSeenAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
