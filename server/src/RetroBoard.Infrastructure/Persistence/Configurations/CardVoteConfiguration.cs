using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class CardVoteConfiguration : IEntityTypeConfiguration<CardVote>
{
    public void Configure(EntityTypeBuilder<CardVote> b)
    {
        b.ToTable("card_votes");
        b.HasKey(x => new { x.CardId, x.SessionId });
        b.Property(x => x.CardId).HasColumnName("card_id");
        b.Property(x => x.SessionId).HasColumnName("session_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
    }
}
