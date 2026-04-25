using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Boards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> b)
    {
        b.ToTable("board_columns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        b.Property(x => x.BoardId).HasColumnName("board_id");
        b.Property(x => x.Position).HasColumnName("position");
        b.Property(x => x.Title).HasColumnName("title").IsRequired();
        b.HasIndex(x => new { x.BoardId, x.Position }).IsUnique();
    }
}
