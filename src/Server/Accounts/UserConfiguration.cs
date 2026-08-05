using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vtt.Server.Accounts;

/// <summary>
/// Maps <see cref="User"/> to the <c>users</c> table.
/// </summary>
/// <remarks>
/// Found by the <c>ApplyConfigurationsFromAssembly</c> call in
/// <c>Infrastructure/VttDbContext.cs</c>, which is why adding this table edits no central file.
/// Column and index names are not written here: the snake_case convention from ADR 004 derives
/// them.
/// </remarks>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Named explicitly, and plural, for two reasons. `user` is a reserved word in PostgreSQL —
        // `SELECT user` returns the session user — so a table of that name needs double quotes in
        // every hand-written query, which is precisely what ADR 004's snake_case decision set out
        // to avoid. And `docs/architecture.md` specifies plural table names throughout.
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(User.UsernameMaxLength)
            .IsRequired();

        builder.Property(user => user.UsernameNormalized)
            .HasMaxLength(User.UsernameMaxLength)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .IsRequired();

        builder.Property(user => user.State)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        // Text, for the same reason as the state: this database gets read by hand, and a role
        // column containing 1 would undo ADR 004's whole argument.
        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        // The database is what actually enforces uniqueness. Checking for an existing username in
        // application code and then inserting leaves a window between the two statements in which a
        // concurrent registration can take the same name; a unique index has no such window.
        builder.HasIndex(user => user.UsernameNormalized).IsUnique();
    }
}
