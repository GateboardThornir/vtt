using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Server.Accounts;

namespace Vtt.Server.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(notification => notification.Subject).HasMaxLength(Notification.SubjectMaxLength);
        builder.Property(notification => notification.CreatedAt).IsRequired();

        // Every read is "mine, newest first", so the index matches that exactly.
        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
