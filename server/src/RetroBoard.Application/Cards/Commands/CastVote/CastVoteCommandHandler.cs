using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Application.Cards.Commands.CastVote;

public class CastVoteCommandHandler(IBoardDbContext db, IPublisher publisher, IClock clock)
    : IRequestHandler<CastVoteCommand, VoteResultDto>
{
    public async Task<VoteResultDto> Handle(CastVoteCommand cmd, CancellationToken ct)
    {
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == cmd.CardId &&
                db.Boards.Any(b => b.Slug == cmd.Slug && b.Id == c.BoardId), ct)
            ?? throw new NotFoundException("Card not found");

        var alreadyVoted = await db.CardVotes
            .AnyAsync(v => v.CardId == cmd.CardId && v.SessionId == cmd.SessionId, ct);

        var inserted = false;
        if (!alreadyVoted)
        {
            try
            {
                db.CardVotes.Add(new CardVote
                {
                    CardId = cmd.CardId,
                    SessionId = cmd.SessionId,
                    CreatedAt = clock.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                inserted = true;
            }
            catch (DbUpdateException)
            {
                // Concurrent insert from same session — composite PK rejected. Treat as already voted.
                inserted = false;
            }
        }

        var votes = await db.CardVotes.CountAsync(v => v.CardId == cmd.CardId, ct);
        if (inserted)
            await publisher.Publish(new VoteCastNotification(cmd.Slug, cmd.CardId, votes, cmd.SessionId), ct);
        return new VoteResultDto(inserted, votes);
    }
}
