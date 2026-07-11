using CRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Infrastructure.Persistence.Configurations;

public class TelegramChatConfiguration : IEntityTypeConfiguration<TelegramChat>
{
    public void Configure(EntityTypeBuilder<TelegramChat> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.TelegramChatId).IsUnique(false);
        builder.HasIndex(c => c.TelegramUserId);
        builder.Property(c => c.TelegramChatId).HasMaxLength(64);
        builder.Property(c => c.TelegramUserId).HasMaxLength(64);
        builder.Property(c => c.FirstName).HasMaxLength(200);
        builder.Property(c => c.LastName).HasMaxLength(200);
        builder.Property(c => c.Username).HasMaxLength(200);
        builder.Property(c => c.PhoneNumber).HasMaxLength(50);

        // ContactId is optional: a chat can exist before it is linked to a Contact.
        builder.HasOne(c => c.Contact)
            .WithMany()
            .HasForeignKey(c => c.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.TelegramChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TelegramMessageConfiguration : IEntityTypeConfiguration<TelegramMessage>
{
    public void Configure(EntityTypeBuilder<TelegramMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.TelegramChatId);
        builder.HasIndex(m => m.ContactId);
        builder.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.TelegramMessageId).HasMaxLength(64);

        builder.HasOne(m => m.Contact)
            .WithMany()
            .HasForeignKey(m => m.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class IntegrationSettingConfiguration : IEntityTypeConfiguration<IntegrationSetting>
{
    public void Configure(EntityTypeBuilder<IntegrationSetting> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.Key).IsUnique();
        builder.Property(s => s.Key).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Category).HasMaxLength(50);
        builder.Property(s => s.Value).HasMaxLength(4000);
    }
}

public class GmailAccountConfiguration : IEntityTypeConfiguration<GmailAccount>
{
    public void Configure(EntityTypeBuilder<GmailAccount> builder)
    {
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => g.EmailAddress).IsUnique();
        builder.Property(g => g.EmailAddress).IsRequired().HasMaxLength(320);
        builder.Property(g => g.EncryptedAccessToken).HasMaxLength(4000);
        builder.Property(g => g.EncryptedRefreshToken).HasMaxLength(4000);

        builder.HasOne(g => g.User)
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.GmailMessageId);
        builder.HasIndex(e => e.ContactId);
        builder.Property(e => e.Subject).HasMaxLength(500);
        builder.Property(e => e.Snippet).HasMaxLength(1000);
        builder.Property(e => e.FromAddress).HasMaxLength(320);
        builder.Property(e => e.ToAddress).HasMaxLength(320);

        builder.HasOne(e => e.GmailAccount)
            .WithMany(g => g.Messages)
            .HasForeignKey(e => e.GmailAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Contact)
            .WithMany()
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CallLogConfiguration : IEntityTypeConfiguration<CallLog>
{
    public void Configure(EntityTypeBuilder<CallLog> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.ProviderCallSid);
        builder.HasIndex(c => c.ContactId);
        builder.Property(c => c.FromNumber).IsRequired().HasMaxLength(30);
        builder.Property(c => c.ToNumber).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Direction).HasConversion<int>();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.ProviderCallSid).HasMaxLength(64);
        builder.Property(c => c.RecordingUrl).HasMaxLength(1000);

        builder.HasOne(c => c.InitiatedByUser)
            .WithMany()
            .HasForeignKey(c => c.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Contact)
            .WithMany()
            .HasForeignKey(c => c.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
