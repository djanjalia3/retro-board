using Microsoft.EntityFrameworkCore;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Application.Common.Abstractions;

public interface IBoardDbContext
{
    DbSet<Board> Boards { get; }
    DbSet<BoardColumn> BoardColumns { get; }
    DbSet<Card> Cards { get; }
    DbSet<CardVote> CardVotes { get; }
    DbSet<Participant> Participants { get; }
    DbSet<ParticipantConnection> ParticipantConnections { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
