using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vtt.Server.Accounts;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        // Named explicitly. EF does not pluralise, and deriving the name from the entity is how
        // task 010 ended up generating `user`, a PostgreSQL reserved word.
        builder.ToTable("invites");

        builder.HasKey(invite => invite.Id);

        builder.Property(invite => invite.TokenHash)
            .HasMaxLength(InviteToken.HashLength)
            .IsRequired();

        builder.Property(invite => invite.CreatedAt).IsRequired();
        builder.Property(invite => invite.ExpiresAt).IsRequired();

        // Redemption looks the invite up by hash, so this index is on the read path as well as
        // being the guarantee that one token cannot exist twice.
        builder.HasIndex(invite => invite.TokenHash).IsUnique();

        // Restrict rather than cascade: accounts are disabled, never deleted, so a delete that
        // would orphan an invite means something has gone wrong and should fail loudly.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invite => invite.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invite => invite.ConsumedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
