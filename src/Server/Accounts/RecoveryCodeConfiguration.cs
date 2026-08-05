using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vtt.Server.Accounts;

public class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        // Named explicitly: EF does not pluralise, and deriving names is how task 010 generated
        // `user`, a reserved word.
        builder.ToTable("recovery_codes");

        builder.HasKey(code => code.Id);

        builder.Property(code => code.CodeHash)
            .HasMaxLength(SecureToken.HashLength)
            .IsRequired();

        builder.Property(code => code.CreatedAt).IsRequired();
        builder.Property(code => code.ExpiresAt).IsRequired();

        builder.HasIndex(code => code.CodeHash).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(code => code.IssuedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
