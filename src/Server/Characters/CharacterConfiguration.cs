using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;

namespace Vtt.Server.Characters;

public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("characters");

        builder.HasKey(character => character.Id);

        builder.Property(character => character.Name).HasMaxLength(Character.NameMaxLength).IsRequired();

        // The first JSONB column, and the first time ADR 004's rule matters: mapped explicitly
        // rather than inferred, so the storage decision is visible where it is made.
        builder.Property(character => character.Sheet).HasColumnType("jsonb").IsRequired();

        builder.Property(character => character.CreatedAt).IsRequired();
        builder.Property(character => character.UpdatedAt).IsRequired();

        builder.HasIndex(character => new { character.CampaignId, character.OwnerUserId });

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(character => character.CampaignId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(character => character.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
