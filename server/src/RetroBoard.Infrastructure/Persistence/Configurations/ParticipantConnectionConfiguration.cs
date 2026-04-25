using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class ParticipantConnectionConfiguration : IEntityTypeConfiguration<ParticipantConnection>
{
    public void Configure(EntityTypeBuilder<ParticipantConnection> b)
    {
        b.ToTable("participant_connections");
        b.HasKey(x => new { x.ParticipantId, x.ConnectionId });
        b.Property(x => x.ParticipantId).HasColumnName("participant_id");
        b.Property(x => x.ConnectionId).HasColumnName("connection_id");
        b.Property(x => x.SessionId).HasColumnName("session_id").IsRequired();
        b.Property(x => x.ConnectedAt).HasColumnName("connected_at").HasDefaultValueSql("now()");
        b.HasIndex(x => x.ConnectionId);
    }
}
