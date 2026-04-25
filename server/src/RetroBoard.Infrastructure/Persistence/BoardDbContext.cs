using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Infrastructure.Persistence;

public class BoardDbContext(DbContextOptions<BoardDbContext> options)
    : DbContext(options), IBoardDbContext
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardVote> CardVotes => Set<CardVote>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<ParticipantConnection> ParticipantConnections => Set<ParticipantConnection>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(BoardDbContext).Assembly);
    }
}
