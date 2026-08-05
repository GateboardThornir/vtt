using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Server.Accounts;
using Vtt.Server.Sessions;

namespace Vtt.Server.Table;

public class RollConfiguration : IEntityTypeConfiguration<Roll>
{
    public void Configure(EntityTypeBuilder<Roll> builder)
    {
        builder.ToTable("rolls");

        builder.HasKey(roll => roll.Id);

        builder.Property(roll => roll.Expression).HasMaxLength(Roll.ExpressionMaxLength).IsRequired();
        builder.Property(roll => roll.Kept).HasMaxLength(500).IsRequired();
        builder.Property(roll => roll.Dropped).HasMaxLength(500).IsRequired();
        builder.Property(roll => roll.Visibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(roll => roll.CreatedAt).IsRequired();

        builder.HasIndex(roll => new { roll.SessionId, roll.CreatedAt });

        builder.HasOne<PlaySession>()
            .WithMany()
            .HasForeignKey(roll => roll.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(roll => roll.RollerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
