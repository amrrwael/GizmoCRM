using CRM.Domain.Entities;
using CRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Role).HasConversion<int>();

        builder.HasMany(u => u.OwnedDeals)
            .WithOne(d => d.Owner)
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Activities)
            .WithOne(a => a.AssignedTo)
            .HasForeignKey(a => a.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.AssignedContacts)
            .WithOne(c => c.AssignedTo)
            .HasForeignKey(c => c.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Email).IsUnique();
        builder.Property(c => c.Email).IsRequired().HasMaxLength(200);
        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Company).HasMaxLength(200);
        builder.Property(c => c.Position).HasMaxLength(200);

        // Map private _tags field
        builder.Property<string>("_tags")
            .HasColumnName("Tags")
            .HasMaxLength(1000)
            .HasDefaultValue(string.Empty);

        builder.Ignore(c => c.Tags);

        builder.HasMany(c => c.Deals)
            .WithOne(d => d.Contact)
            .HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Activities)
            .WithOne(a => a.Contact)
            .HasForeignKey(a => a.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.OwnerId);
        builder.HasIndex(d => d.ContactId);
        builder.HasIndex(d => d.Stage);

        builder.Property(d => d.Title).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Value).HasColumnType("decimal(18,2)");
        builder.Property(d => d.Stage).HasConversion<int>();
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.LostReason).HasMaxLength(500);

        builder.HasMany(d => d.Activities)
            .WithOne(a => a.Deal)
            .HasForeignKey(a => a.DealId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.AssignedToId);
        builder.HasIndex(a => a.ContactId);
        builder.HasIndex(a => a.DealId);
        builder.HasIndex(a => a.DueDate);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.Type).HasConversion<int>();
        builder.Property(a => a.Status).HasConversion<int>();
        builder.Property(a => a.Outcome).HasMaxLength(1000);
    }
}