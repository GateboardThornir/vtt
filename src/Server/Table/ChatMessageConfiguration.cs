using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vtt.Server.Accounts;
using Vtt.Server.Sessions;

namespace Vtt.Server.Table;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Body).HasMaxLength(ChatMessage.BodyMaxLength).IsRequired();
        builder.Property(message => message.Voice).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();

        // Every read is "this session, newest last", so the index matches it.
        builder.HasIndex(message => new { message.SessionId, message.CreatedAt });

        builder.HasOne<PlaySession>()
            .WithMany()
            .HasForeignKey(message => message.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(message => message.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
