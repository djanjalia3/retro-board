using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetroBoard.Domain.Boards;

namespace RetroBoard.Infrastructure.Persistence.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> b)
    {
        b.ToTable("boards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn().HasColumnName("id");
        b.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasMany(x => x.Columns).WithOne().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Cards).WithOne().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.BoardId).OnDelete(DeleteBehavior.Cascade);
    }
}
