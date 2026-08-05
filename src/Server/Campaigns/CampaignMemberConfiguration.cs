using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Server.Accounts;

namespace Vtt.Server.Campaigns;

public class CampaignMemberConfiguration : IEntityTypeConfiguration<CampaignMember>
{
    public void Configure(EntityTypeBuilder<CampaignMember> builder)
    {
        builder.ToTable("campaign_members");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(member => member.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(member => member.CreatedAt).IsRequired();

        // One row per account per campaign. Inviting somebody twice reuses their row rather than
        // creating a second, and the database is what guarantees it.
        builder.HasIndex(member => new { member.CampaignId, member.UserId }).IsUnique();

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(member => member.CampaignId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
