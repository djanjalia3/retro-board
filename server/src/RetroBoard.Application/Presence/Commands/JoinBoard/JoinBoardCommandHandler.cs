using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Application.Presence.Notifications;
using RetroBoard.Domain.Common;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Application.Presence.Commands.JoinBoard;

public class JoinBoardCommandHandler(IBoardDbContext db, IClock clock, IPublisher publisher)
    : IRequestHandler<JoinBoardCommand, JoinBoardResult>
{
    public async Task<JoinBoardResult> Handle(JoinBoardCommand cmd, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Slug == cmd.Slug, ct)
            ?? throw new NotFoundException($"Board '{cmd.Slug}' not found");

        var key = ParticipantKeyFactory.Create(cmd.DisplayName);
        var participant = await db.Participants
            .FirstOrDefaultAsync(p => p.BoardId == board.Id && p.ParticipantKey == key, ct);
        if (participant is null)
        {
            participant = new Participant
            {
                BoardId = board.Id,
                ParticipantKey = key,
                DisplayName = cmd.DisplayName,
                JoinedAt = clock.UtcNow,
                LastSeenAt = clock.UtcNow,
            };
            db.Participants.Add(participant);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            participant.LastSeenAt = clock.UtcNow;
            participant.DisplayName = cmd.DisplayName;
        }

        var existing = await db.ParticipantConnections
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.ConnectionId == cmd.ConnectionId, ct);
        if (existing is null)
        {
            db.ParticipantConnections.Add(new ParticipantConnection
            {
                ParticipantId = participant.Id,
                ConnectionId = cmd.ConnectionId,
                SessionId = cmd.SessionId,
                ConnectedAt = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);

        var participants = await LoadParticipantsAsync(db, board.Id, ct);
        await publisher.Publish(new PresenceChangedNotification(cmd.Slug, participants), ct);
        return new JoinBoardResult(participants);
    }

    internal static async Task<IReadOnlyList<ParticipantDto>> LoadParticipantsAsync(
        IBoardDbContext db, long boardId, CancellationToken ct)
    {
        var rows = await db.Participants
            .AsNoTracking()
            .Where(p => p.BoardId == boardId)
            .Select(p => new
            {
                p.ParticipantKey,
                p.DisplayName,
                p.JoinedAt,
                p.LastSeenAt,
                ConnectionCount = p.Connections.Count,
            })
            .ToListAsync(ct);
        return rows
            .Select(r => new ParticipantDto(r.ParticipantKey, r.DisplayName, r.JoinedAt, r.LastSeenAt, r.ConnectionCount))
            .ToList();
    }
}
