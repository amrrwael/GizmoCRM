using CRM.Application.Features.Dashboard.Queries;
using CRM.Domain.Enums;
using CRM.Tests.Common;
using FluentAssertions;
using Xunit;

namespace CRM.Tests.Application.Dashboard;

public class GetDashboardHandlerTests
{
    [Fact]
    public async Task GetDashboard_AsAdmin_ShouldReturnFullStats()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.Summary.TotalContacts.Should().Be(5);
        result.Summary.TotalDeals.Should().Be(5);
        result.Summary.OpenDeals.Should().Be(3); // Lead/Qualified/Proposal/Negotiation — won and lost excluded
        result.Summary.TotalActivities.Should().Be(4);
        result.Summary.PendingActivities.Should().Be(3); // CompletedCall is done
    }

    [Fact]
    public async Task GetDashboard_AsSales_ShouldReturnPersonalStats()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep1 has: Alice+Bob contacts, 3 deals, 2 activities
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.Summary.TotalContacts.Should().Be(2);
        result.Summary.TotalDeals.Should().Be(3);
        result.TopSalesReps.Should().BeEmpty(); // Sales can't see team reps list
    }

    [Fact]
    public async Task GetDashboard_AsAdmin_ShouldIncludeTopSalesReps()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.TopSalesReps.Should().NotBeEmpty();
        // SalesRep2 won a deal (Nexus — 30000), SalesRep1 has 0 won
        var rep2 = result.TopSalesReps.FirstOrDefault(r => r.FullName == "Ryan Torres");
        rep2.Should().NotBeNull();
        rep2!.WonDeals.Should().Be(1);
        rep2.WonValue.Should().Be(30000m);
    }

    [Fact]
    public async Task GetDashboard_ShouldComputeCorrectPipelineValue()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Open deals: AcmeDeal(75000) + TechStartDeal(12500) + GlobalCoDeal(150000) = 237500
        result.TotalPipelineValue.Should().Be(237500m);
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnOverdueCount()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Only OverdueTask is past due and pending
        result.OverdueActivitiesCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnDealsByStage()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.DealsByStage.Should().NotBeEmpty();
        result.DealsByStage.Should().Contain(s => s.Stage == DealStage.Won && s.Count == 1);
        result.DealsByStage.Should().Contain(s => s.Stage == DealStage.Lost && s.Count == 1);
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnUpcomingActivities()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // CallActivity (tomorrow) and MeetingActivity (4 days) are upcoming
        result.UpcomingActivities.Should().HaveCountGreaterThanOrEqualTo(2);
        result.UpcomingActivities.Should().AllSatisfy(a =>
            a.DueDate.Should().BeAfter(DateTime.UtcNow));
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnRecentActivities()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDashboardHandler(db, currentUser);

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.RecentActivities.Should().NotBeEmpty();
        result.RecentActivities.Should().HaveCountLessOrEqualTo(10);
    }
}
