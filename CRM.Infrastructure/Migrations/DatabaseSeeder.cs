using CRM.Domain.Entities;
using CRM.Domain.Enums;
using CRM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Infrastructure.Migrations;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            await db.Database.MigrateAsync();
            await SeedUsersAsync(db, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Users.AnyAsync()) return;

        logger.LogInformation("Seeding initial users...");

        var admin = User.Create(
            email: "admin@gizmocrm.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
            firstName: "System",
            lastName: "Admin",
            role: UserRole.Admin);

        var manager = User.Create(
            email: "manager@gizmocrm.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Manager@1234"),
            firstName: "Sales",
            lastName: "Manager",
            role: UserRole.Manager);

        var sales = User.Create(
            email: "sales@gizmocrm.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Sales@1234"),
            firstName: "John",
            lastName: "Sales",
            role: UserRole.Sales);

        db.Users.AddRange(admin, manager, sales);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded users: admin@gizmocrm.com / Manager@1234 / Sales@1234");

        // Sample contacts
        var contact1 = Contact.Create("Alice", "Johnson", "alice@acmecorp.com", "+1-555-0101", "Acme Corp", "CEO", admin.Id);
        var contact2 = Contact.Create("Bob", "Williams", "bob@techstart.io", "+1-555-0102", "TechStart", "CTO", admin.Id);
        var contact3 = Contact.Create("Carol", "Davis", "carol@globalco.com", "+1-555-0103", "GlobalCo", "Procurement", admin.Id);

        contact1.AssignTo(sales.Id);
        contact2.AssignTo(sales.Id);
        contact1.SetTags(["enterprise", "hot-lead"]);
        contact2.SetTags(["startup", "tech"]);

        db.Contacts.AddRange(contact1, contact2, contact3);
        await db.SaveChangesAsync();

        // Sample deals
        var deal1 = Deal.Create("Acme Enterprise License", 25000m, sales.Id, contact1.Id,
            DateTime.UtcNow.AddMonths(1), "Annual enterprise software license", admin.Id);
        deal1.MoveToStage(DealStage.Proposal);

        var deal2 = Deal.Create("TechStart Starter Pack", 5000m, sales.Id, contact2.Id,
            DateTime.UtcNow.AddDays(14), "Startup onboarding package", admin.Id);
        deal2.MoveToStage(DealStage.Qualified);

        var deal3 = Deal.Create("GlobalCo Integration", 50000m, sales.Id, contact3.Id,
            DateTime.UtcNow.AddMonths(3), "Full ERP integration project", admin.Id);

        db.Deals.AddRange(deal1, deal2, deal3);
        await db.SaveChangesAsync();

        // Sample activities
        var act1 = Activity.Create(ActivityType.Call, "Discovery call with Alice",
            "Discuss enterprise requirements", DateTime.UtcNow.AddDays(1),
            sales.Id, contact1.Id, deal1.Id, admin.Id);

        var act2 = Activity.Create(ActivityType.Meeting, "Product demo for TechStart",
            "Show core features", DateTime.UtcNow.AddDays(3),
            sales.Id, contact2.Id, deal2.Id, admin.Id);

        var act3 = Activity.Create(ActivityType.Task, "Send proposal to GlobalCo",
            "Prepare detailed pricing", DateTime.UtcNow.AddDays(-1),
            sales.Id, contact3.Id, deal3.Id, admin.Id);

        db.Activities.AddRange(act1, act2, act3);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded sample contacts, deals, and activities.");
    }
}