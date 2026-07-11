using CRM.Domain.Entities;
using CRM.Domain.Enums;
using CRM.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Infrastructure;

/// <summary>
/// Tests that verify EF Core configuration: constraints, relationships,
/// cascade rules, and that the schema behaves as designed.
/// </summary>
public class AppDbContextTests
{
    // ── Unique constraints ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveUser_DuplicateEmail_ShouldThrowDbUpdateException()
    {
        await using var db = TestDbContextFactory.Create();

        var user1 = User.Create("same@email.com", "hash", "Alice", "Smith", UserRole.Sales);
        var user2 = User.Create("same@email.com", "hash", "Alice", "Clone", UserRole.Sales);
        db.Users.AddRange(user1, user2);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>(); // EF InMemory throws on unique index violation
    }

    [Fact]
    public async Task SaveContact_DuplicateEmail_ShouldThrowDbUpdateException()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var duplicate = Contact.Create("Dupe", "Contact", "alice.johnson@acmecorp.com",
            null, null, null, seed.Admin.Id);
        db.Contacts.Add(duplicate);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>();
    }

    // ── Relationship navigation ────────────────────────────────────────────────

    [Fact]
    public async Task Deal_ShouldNavigateToOwnerAndContact()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var deal = await db.Deals
            .Include(d => d.Owner)
            .Include(d => d.Contact)
            .FirstAsync(d => d.Id == seed.AcmeDeal.Id);

        deal.Owner.Should().NotBeNull();
        deal.Owner.FullName.Should().Be("Olivia Parker");
        deal.Contact.Should().NotBeNull();
        deal.Contact.FullName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task Activity_ShouldNavigateToAssignedToContactAndDeal()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var activity = await db.Activities
            .Include(a => a.AssignedTo)
            .Include(a => a.Contact)
            .Include(a => a.Deal)
            .FirstAsync(a => a.Id == seed.CallActivity.Id);

        activity.AssignedTo.FullName.Should().Be("Olivia Parker");
        activity.Contact!.FullName.Should().Be("Alice Johnson");
        activity.Deal!.Title.Should().Contain("Acme");
    }

    [Fact]
    public async Task Contact_ShouldNavigateToAssignedUser()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var contact = await db.Contacts
            .Include(c => c.AssignedTo)
            .FirstAsync(c => c.Id == seed.AliceJohnson.Id);

        contact.AssignedTo.Should().NotBeNull();
        contact.AssignedTo!.FullName.Should().Be("Olivia Parker");
    }

    // ── Cascade behaviour ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteContact_WhenHasDeals_ShouldBeRestrictedByForeignKey()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        // AliceJohnson has AcmeDeal — EF with Restrict should throw
        db.Contacts.Remove(seed.AliceJohnson);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>();
    }

    // ── Entity persistence round-trip ──────────────────────────────────────────

    [Fact]
    public async Task User_CreatedViaFactory_ShouldRoundTripCorrectly()
    {
        await using var db = TestDbContextFactory.Create();

        var user = User.Create("roundtrip@test.com", "hash", "Round", "Trip", UserRole.Manager);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Load fresh from DB
        db.ChangeTracker.Clear();
        var loaded = await db.Users.FindAsync(user.Id);

        loaded.Should().NotBeNull();
        loaded!.Email.Should().Be("roundtrip@test.com");
        loaded.Role.Should().Be(UserRole.Manager);
        loaded.IsActive.Should().BeTrue();
        loaded.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Contact_Tags_ShouldPersistAndReloadCorrectly()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        // Reload fresh (bypassing tracker cache)
        db.ChangeTracker.Clear();
        var contact = await db.Contacts.FindAsync(seed.AliceJohnson.Id);

        contact!.Tags.Should().Contain("enterprise");
        contact.Tags.Should().Contain("hot-lead");
        contact.Tags.Should().Contain("c-suite");
    }

    [Fact]
    public async Task Deal_Value_ShouldPersistWithDecimalPrecision()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        db.ChangeTracker.Clear();
        var deal = await db.Deals.FindAsync(seed.AcmeDeal.Id);

        deal!.Value.Should().Be(75000.00m);
    }

    [Fact]
    public async Task Activity_Completion_ShouldPersistOutcomeAndTimestamp()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        db.ChangeTracker.Clear();
        var activity = await db.Activities.FindAsync(seed.CompletedCall.Id);

        activity!.Status.Should().Be(ActivityStatus.Completed);
        activity.Outcome.Should().Be("Contract signed. Start date confirmed for next month.");
        activity.CompletedAt.Should().NotBeNull();
    }

    // ── Query filtering via EF ─────────────────────────────────────────────────

    [Fact]
    public async Task QueryDeals_ByStage_ShouldReturnOnlyMatchingRows()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var wonDeals = await db.Deals
            .Where(d => d.Stage == DealStage.Won)
            .ToListAsync();

        wonDeals.Should().ContainSingle()
            .Which.Id.Should().Be(seed.WonDeal.Id);
    }

    [Fact]
    public async Task QueryActivities_PendingAndOverdue_ShouldFilterCorrectly()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var overdue = await db.Activities
            .Where(a => a.Status == ActivityStatus.Pending && a.DueDate < DateTime.UtcNow)
            .ToListAsync();

        overdue.Should().ContainSingle()
            .Which.Id.Should().Be(seed.OverdueTask.Id);
    }

    [Fact]
    public async Task QueryContacts_ByAssignedUser_ShouldFilter()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        var contacts = await db.Contacts
            .Where(c => c.AssignedToId == seed.SalesRep1.Id)
            .ToListAsync();

        contacts.Should().HaveCount(2);
        contacts.Should().AllSatisfy(c => c.AssignedToId.Should().Be(seed.SalesRep1.Id));
    }

    [Fact]
    public async Task QueryUsers_ActiveOnly_ShouldExcludeDeactivated()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        seed.SalesRep2.Deactivate();
        await db.SaveChangesAsync();

        var activeUsers = await db.Users
            .Where(u => u.IsActive)
            .ToListAsync();

        activeUsers.Should().HaveCount(3);
        activeUsers.Should().NotContain(u => u.Id == seed.SalesRep2.Id);
    }
}
