using CRM.Application.Common.Exceptions;
using CRM.Application.Features.Contacts.Commands;
using CRM.Application.Features.Contacts.Queries;
using CRM.Tests.Common;
using FluentAssertions;
using Xunit;

namespace CRM.Tests.Application.Contacts;

public class CreateContactHandlerTests
{
    [Fact]
    public async Task CreateContact_ShouldPersistAndReturnDto()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateContactHandler(db, currentUser);

        var result = await handler.Handle(new CreateContactCommand(
            "Jennifer", "Walsh", "jennifer.walsh@startup.io",
            "+1-415-555-0199", "StartupIO", "VP Sales",
            "Met at SaaStr conference.", ["startup", "saas"]), CancellationToken.None);

        result.FirstName.Should().Be("Jennifer");
        result.LastName.Should().Be("Walsh");
        result.Email.Should().Be("jennifer.walsh@startup.io");
        result.Company.Should().Be("StartupIO");
        result.Tags.Should().Contain("startup").And.Contain("saas");
        result.Notes.Should().Be("Met at SaaStr conference.");
    }

    [Fact]
    public async Task CreateContact_DuplicateEmail_ShouldThrowConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateContactHandler(db, currentUser);

        var act = () => handler.Handle(new CreateContactCommand(
            "Duplicate", "Alice", "alice.johnson@acmecorp.com", // already seeded
            null, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*alice.johnson@acmecorp.com*");
    }

    [Fact]
    public async Task CreateContact_ShouldNormaliseEmailToLowercase()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateContactHandler(db, currentUser);

        var result = await handler.Handle(new CreateContactCommand(
            "Test", "User", "TEST.USER@EXAMPLE.COM",
            null, null, null, null, null), CancellationToken.None);

        result.Email.Should().Be("test.user@example.com");
    }
}

public class UpdateContactHandlerTests
{
    [Fact]
    public async Task UpdateContact_AsAdmin_ShouldUpdateAllFields()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new UpdateContactHandler(db, currentUser);

        var result = await handler.Handle(new UpdateContactCommand(
            seed.AliceJohnson.Id, "Alicia", "Johnson-CEO",
            "alicia.new@acmecorp.com", "+1-212-555-9999",
            "Acme Corporation", "Chief Executive Officer",
            "Updated notes.", ["enterprise", "priority"]), CancellationToken.None);

        result.FirstName.Should().Be("Alicia");
        result.LastName.Should().Be("Johnson-CEO");
        result.Phone.Should().Be("+1-212-555-9999");
        result.Notes.Should().Be("Updated notes.");
    }

    [Fact]
    public async Task UpdateContact_SalesAccessingAssignedContact_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep1 is assigned to AliceJohnson
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new UpdateContactHandler(db, currentUser);

        var result = await handler.Handle(new UpdateContactCommand(
            seed.AliceJohnson.Id, "Alice", "Johnson",
            "alice.johnson@acmecorp.com", "+1-212-555-0101",
            "Acme Corp", "CEO", "Notes updated by sales.", null), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateContact_SalesAccessingOtherContact_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep2 trying to update AliceJohnson (assigned to SalesRep1)
        var currentUser = MockCurrentUser.Sales(seed.SalesRep2.Id);
        var handler = new UpdateContactHandler(db, currentUser);

        var act = () => handler.Handle(new UpdateContactCommand(
            seed.AliceJohnson.Id, "Alice", "Johnson",
            "alice.johnson@acmecorp.com", null, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateContact_NonExistentId_ShouldThrowNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var currentUser = MockCurrentUser.Admin();
        var handler = new UpdateContactHandler(db, currentUser);

        var act = () => handler.Handle(new UpdateContactCommand(
            Guid.NewGuid(), "X", "Y", "x@y.com", null, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class GetContactsQueryHandlerTests
{
    [Fact]
    public async Task GetContacts_AsAdmin_ShouldReturnAllContacts()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var result = await handler.Handle(new GetContactsQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetContacts_AsSales_ShouldReturnOnlyAssignedContacts()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep1 is assigned to Alice and Bob (2 contacts)
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var result = await handler.Handle(new GetContactsQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(c =>
            c.AssignedToId.Should().Be(seed.SalesRep1.Id));
    }

    [Fact]
    public async Task GetContacts_SearchByName_ShouldFilterResults()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var result = await handler.Handle(new GetContactsQuery(Search: "alice"), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.FullName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task GetContacts_SearchByCompany_ShouldFilterResults()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var result = await handler.Handle(new GetContactsQuery(Search: "acme"), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.Company.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task GetContacts_FilterByTag_ShouldReturnMatchingContacts()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var result = await handler.Handle(new GetContactsQuery(Tag: "enterprise"), CancellationToken.None);

        result.Items.Should().HaveCount(2); // Alice and Carol have "enterprise" tag
    }

    [Fact]
    public async Task GetContacts_Pagination_ShouldReturnCorrectPage()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var result = await handler.Handle(new GetContactsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetContacts_SecondPage_ShouldReturnNextItems()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactsHandler(db, currentUser);

        var page1 = await handler.Handle(new GetContactsQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(new GetContactsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        page1.Items.Select(c => c.Id).Should().NotIntersectWith(page2.Items.Select(c => c.Id));
    }
}

public class DeleteContactHandlerTests
{
    [Fact]
    public async Task DeleteContact_AsAdmin_ShouldRemoveContact()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new DeleteContactHandler(db, currentUser);

        await handler.Handle(new DeleteContactCommand(seed.EmmaBrown.Id), CancellationToken.None);

        db.Contacts.Should().NotContain(c => c.Id == seed.EmmaBrown.Id);
    }

    [Fact]
    public async Task DeleteContact_AsSales_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new DeleteContactHandler(db, currentUser);

        var act = () => handler.Handle(
            new DeleteContactCommand(seed.AliceJohnson.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

public class AssignContactHandlerTests
{
    [Fact]
    public async Task AssignContact_AsAdmin_ShouldUpdateAssignedTo()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new AssignContactHandler(db, currentUser);

        var result = await handler.Handle(
            new AssignContactCommand(seed.EmmaBrown.Id, seed.SalesRep1.Id),
            CancellationToken.None);

        result.AssignedToId.Should().Be(seed.SalesRep1.Id);
    }

    [Fact]
    public async Task AssignContact_AsSales_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new AssignContactHandler(db, currentUser);

        var act = () => handler.Handle(
            new AssignContactCommand(seed.EmmaBrown.Id, seed.SalesRep2.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AssignContact_ToNonExistentUser_ShouldThrowNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new AssignContactHandler(db, currentUser);

        var act = () => handler.Handle(
            new AssignContactCommand(seed.EmmaBrown.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class GetContactTimelineTests
{
    [Fact]
    public async Task GetTimeline_ShouldReturnActivitiesAndDeals()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetContactTimelineHandler(db, currentUser);

        var result = await handler.Handle(
            new GetContactTimelineQuery(seed.AliceJohnson.Id),
            CancellationToken.None);

        // Alice has 1 activity (CallActivity) and 1 deal (AcmeDeal)
        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(i => i.ItemType == "Activity");
        result.Items.Should().Contain(i => i.ItemType == "Deal");
    }

    [Fact]
    public async Task GetTimeline_SalesSeeingOtherContact_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep2 trying to see Alice's timeline (assigned to SalesRep1)
        var currentUser = MockCurrentUser.Sales(seed.SalesRep2.Id);
        var handler = new GetContactTimelineHandler(db, currentUser);

        var act = () => handler.Handle(
            new GetContactTimelineQuery(seed.AliceJohnson.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
