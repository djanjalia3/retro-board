using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> b)
    {
        b.ToTable("cards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.BoardId).HasColumnName("board_id");
        b.Property(x => x.ColumnId).HasColumnName("column_id");
        b.Property(x => x.Text).HasColumnName("text").IsRequired();
        b.Property(x => x.Author).HasColumnName("author").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.HasIndex(x => new { x.BoardId, x.CreatedAt });
        b.HasOne<Domain.Boards.BoardColumn>().WithMany().HasForeignKey(x => x.ColumnId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Votes).WithOne().HasForeignKey(v => v.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}
