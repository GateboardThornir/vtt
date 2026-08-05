using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vtt.Server.Campaigns;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.HasKey(campaign => campaign.Id);

        builder.Property(campaign => campaign.Name).HasMaxLength(Campaign.NameMaxLength).IsRequired();
        builder.Property(campaign => campaign.SystemId).HasMaxLength(Campaign.SystemIdMaxLength).IsRequired();
        builder.Property(campaign => campaign.SystemVersion).HasMaxLength(Campaign.SystemVersionMaxLength).IsRequired();
        builder.Property(campaign => campaign.CreatedAt).IsRequired();

    }
}
