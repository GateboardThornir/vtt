using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Server.Campaigns;

namespace Vtt.Server.Sessions;

public class PlaySessionConfiguration : IEntityTypeConfiguration<PlaySession>
{
    public void Configure(EntityTypeBuilder<PlaySession> builder)
    {
        builder.ToTable("play_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Title).HasMaxLength(PlaySession.TitleMaxLength).IsRequired();
        builder.Property(session => session.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(session => session.CreatedAt).IsRequired();

        builder.HasIndex(session => new { session.CampaignId, session.CreatedAt });

        // At most one open session per campaign, guaranteed by a partial unique index rather than
        // by a check before the write. Third time this shape has come up — usernames, invites, and
        // now this — and the answer is the same each time: the window between reading and writing
        // is where concurrency lives, so the database has to be the one that decides.
        builder.HasIndex(session => session.CampaignId)
            .IsUnique()
            .HasFilter("state = 'Open'")
            .HasDatabaseName("ux_play_sessions_one_open_per_campaign");

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(session => session.CampaignId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
