using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> b)
    {
        b.ToTable("participants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        b.Property(x => x.BoardId).HasColumnName("board_id");
        b.Property(x => x.ParticipantKey).HasColumnName("participant_key").IsRequired();
        b.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        b.Property(x => x.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");
        b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasDefaultValueSql("now()");
        b.HasIndex(x => new { x.BoardId, x.ParticipantKey }).IsUnique();
        b.HasMany(x => x.Connections).WithOne().HasForeignKey(c => c.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}
