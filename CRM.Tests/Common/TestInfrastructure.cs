using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using CRM.Domain.Enums;
using CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CRM.Tests.Common;

/// <summary>
/// Creates a fresh in-memory EF Core database for each test.
/// Each test class gets an isolated DbContext — no shared state.
/// </summary>
public static class TestDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}

/// <summary>
/// Pre-built CurrentUserService mocks for each role.
/// </summary>
public static class MockCurrentUser
{
    public static ICurrentUserService Admin(Guid? userId = null)
        => Build(userId ?? Guid.NewGuid(), UserRole.Admin);

    public static ICurrentUserService Manager(Guid? userId = null)
        => Build(userId ?? Guid.NewGuid(), UserRole.Manager);

    public static ICurrentUserService Sales(Guid? userId = null)
        => Build(userId ?? Guid.NewGuid(), UserRole.Sales);

    public static ICurrentUserService As(Guid userId, UserRole role)
        => Build(userId, role);

    private static ICurrentUserService Build(Guid userId, UserRole role)
    {
        var mock = Substitute.For<ICurrentUserService>();
        mock.UserId.Returns(userId);
        mock.Role.Returns(role);
        mock.IsAuthenticated.Returns(true);
        mock.Email.Returns($"{role.ToString().ToLower()}@test.com");
        return mock;
    }
}

/// <summary>
/// Real-world seed data that tests actually interact with.
/// Call SeedAsync() once in test setup, then use the returned Ids.
/// </summary>
public class SeedData
{
    public User Admin { get; private set; } = null!;
    public User Manager { get; private set; } = null!;
    public User SalesRep1 { get; private set; } = null!;
    public User SalesRep2 { get; private set; } = null!;

    public Contact AliceJohnson { get; private set; } = null!;
    public Contact BobWilliams { get; private set; } = null!;
    public Contact CarolDavis { get; private set; } = null!;
    public Contact DavidLee { get; private set; } = null!;
    public Contact EmmaBrown { get; private set; } = null!;

    public Deal AcmeDeal { get; private set; } = null!;
    public Deal TechStartDeal { get; private set; } = null!;
    public Deal GlobalCoDeal { get; private set; } = null!;
    public Deal WonDeal { get; private set; } = null!;
    public Deal LostDeal { get; private set; } = null!;

    public Activity CallActivity { get; private set; } = null!;
    public Activity MeetingActivity { get; private set; } = null!;
    public Activity OverdueTask { get; private set; } = null!;
    public Activity CompletedCall { get; private set; } = null!;

    public static async Task<SeedData> CreateAsync(AppDbContext db)
    {
        var seed = new SeedData();

        // ── Users ──────────────────────────────────────────────────────────────
        seed.Admin = User.Create(
            "sarah.chen@gizmocrm.com",
            BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
            "Sarah", "Chen", UserRole.Admin);

        seed.Manager = User.Create(
            "james.morrison@gizmocrm.com",
            BCrypt.Net.BCrypt.HashPassword("Manager@1234"),
            "James", "Morrison", UserRole.Manager);

        seed.SalesRep1 = User.Create(
            "olivia.parker@gizmocrm.com",
            BCrypt.Net.BCrypt.HashPassword("Sales@1234"),
            "Olivia", "Parker", UserRole.Sales);

        seed.SalesRep2 = User.Create(
            "ryan.torres@gizmocrm.com",
            BCrypt.Net.BCrypt.HashPassword("Sales@1234"),
            "Ryan", "Torres", UserRole.Sales);

        db.Users.AddRange(seed.Admin, seed.Manager, seed.SalesRep1, seed.SalesRep2);
        await db.SaveChangesAsync();

        // ── Contacts ───────────────────────────────────────────────────────────
        seed.AliceJohnson = Contact.Create(
            "Alice", "Johnson", "alice.johnson@acmecorp.com",
            "+1-212-555-0101", "Acme Corp", "Chief Executive Officer", seed.Admin.Id);
        seed.AliceJohnson.AssignTo(seed.SalesRep1.Id);
        seed.AliceJohnson.SetTags(["enterprise", "hot-lead", "c-suite"]);

        seed.BobWilliams = Contact.Create(
            "Bob", "Williams", "bob.williams@techstart.io",
            "+1-415-555-0102", "TechStart Inc", "Chief Technology Officer", seed.Admin.Id);
        seed.BobWilliams.AssignTo(seed.SalesRep1.Id);
        seed.BobWilliams.SetTags(["startup", "tech", "saas"]);

        seed.CarolDavis = Contact.Create(
            "Carol", "Davis", "carol.davis@globalco.com",
            "+44-20-7946-0103", "GlobalCo Ltd", "Head of Procurement", seed.Admin.Id);
        seed.CarolDavis.AssignTo(seed.SalesRep2.Id);
        seed.CarolDavis.SetTags(["enterprise", "uk-market"]);

        seed.DavidLee = Contact.Create(
            "David", "Lee", "david.lee@nexusventures.com",
            "+1-650-555-0104", "Nexus Ventures", "Managing Partner", seed.Admin.Id);
        seed.DavidLee.AssignTo(seed.SalesRep2.Id);
        seed.DavidLee.SetTags(["investor", "vip"]);

        seed.EmmaBrown = Contact.Create(
            "Emma", "Brown", "emma.brown@innovatehq.com",
            "+1-312-555-0105", "InnovateHQ", "Product Manager", seed.Manager.Id);
        seed.EmmaBrown.UpdateNotes("Met at SaaS conference. Very interested in integration features.");

        db.Contacts.AddRange(
            seed.AliceJohnson, seed.BobWilliams, seed.CarolDavis,
            seed.DavidLee, seed.EmmaBrown);
        await db.SaveChangesAsync();

        // ── Deals ──────────────────────────────────────────────────────────────
        seed.AcmeDeal = Deal.Create(
            "Acme Corp — Enterprise License",
            75000m, seed.SalesRep1.Id, seed.AliceJohnson.Id,
            DateTime.UtcNow.AddMonths(2),
            "Annual enterprise software license for 500 seats.", seed.Admin.Id);
        seed.AcmeDeal.MoveToStage(DealStage.Proposal);

        seed.TechStartDeal = Deal.Create(
            "TechStart — Growth Package",
            12500m, seed.SalesRep1.Id, seed.BobWilliams.Id,
            DateTime.UtcNow.AddDays(21),
            "SaaS platform subscription with API access.", seed.Admin.Id);
        seed.TechStartDeal.MoveToStage(DealStage.Qualified);

        seed.GlobalCoDeal = Deal.Create(
            "GlobalCo — ERP Integration",
            150000m, seed.SalesRep2.Id, seed.CarolDavis.Id,
            DateTime.UtcNow.AddMonths(4),
            "Full ERP system integration and 12-month support.", seed.Admin.Id);
        seed.GlobalCoDeal.MoveToStage(DealStage.Negotiation);

        seed.WonDeal = Deal.Create(
            "Nexus — Consulting Retainer",
            30000m, seed.SalesRep2.Id, seed.DavidLee.Id,
            DateTime.UtcNow.AddDays(-10),
            "Quarterly consulting retainer agreement.", seed.Admin.Id);
        seed.WonDeal.MoveToStage(DealStage.Won);

        seed.LostDeal = Deal.Create(
            "InnovateHQ — Pilot Project",
            8000m, seed.SalesRep1.Id, seed.EmmaBrown.Id,
            DateTime.UtcNow.AddDays(-30),
            "3-month pilot program.", seed.Admin.Id);
        seed.LostDeal.MoveToStage(DealStage.Lost, "Budget constraints — revisit Q3.");

        db.Deals.AddRange(
            seed.AcmeDeal, seed.TechStartDeal, seed.GlobalCoDeal,
            seed.WonDeal, seed.LostDeal);
        await db.SaveChangesAsync();

        // ── Activities ─────────────────────────────────────────────────────────
        seed.CallActivity = Activity.Create(
            ActivityType.Call, "Intro call with Alice Johnson",
            "Discuss enterprise requirements, budget cycle, and decision-making process.",
            DateTime.UtcNow.AddDays(1),
            seed.SalesRep1.Id, seed.AliceJohnson.Id, seed.AcmeDeal.Id, seed.Admin.Id);
        seed.CallActivity.SetReminder(DateTime.UtcNow.AddHours(20));

        seed.MeetingActivity = Activity.Create(
            ActivityType.Meeting, "Product demo for TechStart team",
            "Walk through core CRM features and API integration capabilities.",
            DateTime.UtcNow.AddDays(4),
            seed.SalesRep1.Id, seed.BobWilliams.Id, seed.TechStartDeal.Id, seed.Admin.Id);

        seed.OverdueTask = Activity.Create(
            ActivityType.Task, "Send revised proposal to GlobalCo",
            "Include updated pricing for 3-year contract option.",
            DateTime.UtcNow.AddDays(-2), // overdue
            seed.SalesRep2.Id, seed.CarolDavis.Id, seed.GlobalCoDeal.Id, seed.Admin.Id);

        seed.CompletedCall = Activity.Create(
            ActivityType.Call, "Follow-up call with Nexus Ventures",
            "Confirmed contract terms and start date.",
            DateTime.UtcNow.AddDays(-15),
            seed.SalesRep2.Id, seed.DavidLee.Id, seed.WonDeal.Id, seed.Admin.Id);
        seed.CompletedCall.Complete("Contract signed. Start date confirmed for next month.");

        db.Activities.AddRange(
            seed.CallActivity, seed.MeetingActivity,
            seed.OverdueTask, seed.CompletedCall);
        await db.SaveChangesAsync();

        return seed;
    }
}
