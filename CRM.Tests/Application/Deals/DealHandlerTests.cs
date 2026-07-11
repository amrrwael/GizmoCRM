using CRM.Application.Common.Exceptions;
using CRM.Application.Features.Deals.Commands;
using CRM.Application.Features.Deals.Queries;
using CRM.Domain.Enums;
using CRM.Tests.Common;
using FluentAssertions;
using Xunit;

namespace CRM.Tests.Application.Deals;

public class CreateDealHandlerTests
{
    [Fact]
    public async Task CreateDeal_AsAdmin_ShouldPersistAndReturnDto()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateDealHandler(db, currentUser);

        var result = await handler.Handle(new CreateDealCommand(
            "New Enterprise Opportunity", 95000m,
            seed.DavidLee.Id, seed.SalesRep1.Id,
            DateTime.UtcNow.AddMonths(3), "Expansion opportunity."), CancellationToken.None);

        result.Title.Should().Be("New Enterprise Opportunity");
        result.Value.Should().Be(95000m);
        result.Stage.Should().Be(DealStage.Lead);
        result.Probability.Should().Be(10);
        result.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDeal_SalesForSelf_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new CreateDealHandler(db, currentUser);

        var result = await handler.Handle(new CreateDealCommand(
            "My New Deal", 10000m,
            seed.AliceJohnson.Id, seed.SalesRep1.Id, // own id
            null, null), CancellationToken.None);

        result.OwnerId.Should().Be(seed.SalesRep1.Id);
    }

    [Fact]
    public async Task CreateDeal_SalesForOtherRep_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new CreateDealHandler(db, currentUser);

        var act = () => handler.Handle(new CreateDealCommand(
            "Hijacked Deal", 10000m,
            seed.AliceJohnson.Id, seed.SalesRep2.Id, // different sales rep
            null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateDeal_NonExistentContact_ShouldThrowNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateDealHandler(db, currentUser);

        var act = () => handler.Handle(new CreateDealCommand(
            "Ghost Deal", 1000m, Guid.NewGuid(), null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Contact*");
    }

    [Fact]
    public async Task CreateDeal_WithoutOwnerId_ShouldDefaultToCurrentUser()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep2.Id);
        var handler = new CreateDealHandler(db, currentUser);

        var result = await handler.Handle(new CreateDealCommand(
            "Auto-Assigned Deal", 5000m, seed.CarolDavis.Id, null, null, null), CancellationToken.None);

        result.OwnerId.Should().Be(seed.SalesRep2.Id);
    }
}

public class MoveDealStageHandlerTests
{
    [Fact]
    public async Task MoveStage_Lead_To_Proposal_ShouldUpdateStageAndProbability()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new MoveDealStageHandler(db, currentUser);

        var result = await handler.Handle(
            new MoveDealStageCommand(seed.TechStartDeal.Id, DealStage.Proposal, null),
            CancellationToken.None);

        result.Stage.Should().Be(DealStage.Proposal);
        result.Probability.Should().Be(60);
    }

    [Fact]
    public async Task MoveStage_ToWon_ShouldCloseAndReturnIsOpenFalse()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new MoveDealStageHandler(db, currentUser);

        var result = await handler.Handle(
            new MoveDealStageCommand(seed.AcmeDeal.Id, DealStage.Won, null),
            CancellationToken.None);

        result.Stage.Should().Be(DealStage.Won);
        result.IsOpen.Should().BeFalse();
        result.ClosedAt.Should().NotBeNull();
        result.Probability.Should().Be(100);
    }

    [Fact]
    public async Task MoveStage_ToLost_ShouldStoreLostReason()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new MoveDealStageHandler(db, currentUser);

        var result = await handler.Handle(
            new MoveDealStageCommand(seed.TechStartDeal.Id, DealStage.Lost, "Price was too high."),
            CancellationToken.None);

        result.Stage.Should().Be(DealStage.Lost);
        result.LostReason.Should().Be("Price was too high.");
        result.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task MoveStage_SalesAccessingOthersDeal_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep2 trying to move SalesRep1's deal
        var currentUser = MockCurrentUser.Sales(seed.SalesRep2.Id);
        var handler = new MoveDealStageHandler(db, currentUser);

        var act = () => handler.Handle(
            new MoveDealStageCommand(seed.AcmeDeal.Id, DealStage.Won, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

public class GetDealsQueryHandlerTests
{
    [Fact]
    public async Task GetDeals_AsAdmin_ShouldReturnAllDeals()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDealsHandler(db, currentUser);

        var result = await handler.Handle(new GetDealsQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetDeals_AsSales_ShouldReturnOnlyOwnDeals()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetDealsHandler(db, currentUser);

        var result = await handler.Handle(new GetDealsQuery(), CancellationToken.None);

        // SalesRep1 owns: AcmeDeal, TechStartDeal, LostDeal = 3
        result.TotalCount.Should().Be(3);
        result.Items.Should().AllSatisfy(d => d.OwnerId.Should().Be(seed.SalesRep1.Id));
    }

    [Fact]
    public async Task GetDeals_FilterByStage_ShouldReturnOnlyThatStage()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDealsHandler(db, currentUser);

        var result = await handler.Handle(new GetDealsQuery(Stage: DealStage.Won), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.Stage.Should().Be(DealStage.Won);
    }

    [Fact]
    public async Task GetDeals_SearchByTitle_ShouldFilter()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetDealsHandler(db, currentUser);

        var result = await handler.Handle(new GetDealsQuery(Search: "acme"), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.Title.Should().Contain("Acme");
    }
}

public class GetKanbanBoardHandlerTests
{
    [Fact]
    public async Task GetKanban_AsAdmin_ShouldReturnAllStagesWithCorrectCounts()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetKanbanBoardHandler(db, currentUser);

        var result = await handler.Handle(new GetKanbanBoardQuery(), CancellationToken.None);

        result.Proposal.Count.Should().Be(1);
        result.Qualified.Count.Should().Be(1);
        result.Negotiation.Count.Should().Be(1);
        result.Won.Count.Should().Be(1);
        result.Lost.Count.Should().Be(1);
        result.Lead.Count.Should().Be(0); // all seeded deals were moved from Lead
    }

    [Fact]
    public async Task GetKanban_AsSales_ShouldOnlyShowOwnDeals()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep1 owns: AcmeDeal(Proposal), TechStartDeal(Qualified), LostDeal(Lost)
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetKanbanBoardHandler(db, currentUser);

        var result = await handler.Handle(new GetKanbanBoardQuery(), CancellationToken.None);

        var totalDeals = result.Lead.Count + result.Qualified.Count + result.Proposal.Count
            + result.Negotiation.Count + result.Won.Count + result.Lost.Count;
        totalDeals.Should().Be(3);
    }

    [Fact]
    public async Task GetKanban_ShouldComputeColumnTotalValues()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetKanbanBoardHandler(db, currentUser);

        var result = await handler.Handle(new GetKanbanBoardQuery(), CancellationToken.None);

        // AcmeDeal = 75000, is in Proposal
        result.Proposal.TotalValue.Should().Be(75000m);
        result.Won.TotalValue.Should().Be(30000m); // WonDeal = 30000
    }
}

public class ReassignDealHandlerTests
{
    [Fact]
    public async Task ReassignDeal_AsManager_ShouldUpdateOwner()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Manager(seed.Manager.Id);
        var handler = new ReassignDealHandler(db, currentUser);

        var result = await handler.Handle(
            new ReassignDealCommand(seed.AcmeDeal.Id, seed.SalesRep2.Id),
            CancellationToken.None);

        result.OwnerId.Should().Be(seed.SalesRep2.Id);
        result.OwnerName.Should().Be("Ryan Torres");
    }

    [Fact]
    public async Task ReassignDeal_AsSales_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new ReassignDealHandler(db, currentUser);

        var act = () => handler.Handle(
            new ReassignDealCommand(seed.AcmeDeal.Id, seed.SalesRep2.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
