using CRM.Application.Common.Exceptions;
using CRM.Application.Features.Activities.Commands;
using CRM.Application.Features.Activities.Queries;
using CRM.Domain.Enums;
using CRM.Tests.Common;
using FluentAssertions;
using Xunit;

namespace CRM.Tests.Application.Activities;

public class CreateActivityHandlerTests
{
    [Fact]
    public async Task CreateActivity_LinkedToContact_ShouldPersistAndReturn()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateActivityHandler(db, currentUser);

        var result = await handler.Handle(new CreateActivityCommand(
            ActivityType.Email, "Send proposal email",
            "Follow up on enterprise pricing.",
            DateTime.UtcNow.AddDays(1),
            seed.SalesRep1.Id,
            seed.AliceJohnson.Id, null, null), CancellationToken.None);

        result.Title.Should().Be("Send proposal email");
        result.Type.Should().Be(ActivityType.Email);
        result.Status.Should().Be(ActivityStatus.Pending);
        result.ContactId.Should().Be(seed.AliceJohnson.Id);
        result.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task CreateActivity_WithReminder_ShouldSetHasReminder()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateActivityHandler(db, currentUser);

        var reminderTime = DateTime.UtcNow.AddHours(4);
        var result = await handler.Handle(new CreateActivityCommand(
            ActivityType.Call, "Reminder Call", null,
            DateTime.UtcNow.AddDays(1),
            seed.SalesRep1.Id,
            seed.AliceJohnson.Id, null, reminderTime), CancellationToken.None);

        result.HasReminder.Should().BeTrue();
        result.ReminderAt.Should().BeCloseTo(reminderTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateActivity_SalesForSelf_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new CreateActivityHandler(db, currentUser);

        var result = await handler.Handle(new CreateActivityCommand(
            ActivityType.Task, "Follow up",
            null, DateTime.UtcNow.AddDays(2),
            seed.SalesRep1.Id, // own id
            seed.AliceJohnson.Id, null, null), CancellationToken.None);

        result.AssignedToId.Should().Be(seed.SalesRep1.Id);
    }

    [Fact]
    public async Task CreateActivity_SalesAssigningToOther_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new CreateActivityHandler(db, currentUser);

        var act = () => handler.Handle(new CreateActivityCommand(
            ActivityType.Task, "Task for someone else", null, null,
            seed.SalesRep2.Id, // different user
            seed.AliceJohnson.Id, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateActivity_NonExistentContact_ShouldThrowNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new CreateActivityHandler(db, currentUser);

        var act = () => handler.Handle(new CreateActivityCommand(
            ActivityType.Call, "Ghost Call", null, null,
            seed.SalesRep1.Id, Guid.NewGuid(), null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class CompleteActivityHandlerTests
{
    [Fact]
    public async Task CompleteActivity_ShouldSetStatusAndOutcome()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new CompleteActivityHandler(db, currentUser);

        var result = await handler.Handle(
            new CompleteActivityCommand(seed.CallActivity.Id, "Agreed on next steps. Demo booked."),
            CancellationToken.None);

        result.Status.Should().Be(ActivityStatus.Completed);
        result.Outcome.Should().Be("Agreed on next steps. Demo booked.");
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteActivity_AlreadyCompleted_ShouldThrowConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep2.Id);
        var handler = new CompleteActivityHandler(db, currentUser);

        // CompletedCall is already completed in seed data
        var act = () => handler.Handle(
            new CompleteActivityCommand(seed.CompletedCall.Id, "Again?"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already completed*");
    }

    [Fact]
    public async Task CompleteActivity_WrongSalesUser_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep2 trying to complete SalesRep1's activity
        var currentUser = MockCurrentUser.Sales(seed.SalesRep2.Id);
        var handler = new CompleteActivityHandler(db, currentUser);

        var act = () => handler.Handle(
            new CompleteActivityCommand(seed.CallActivity.Id, "Sneaky"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

public class CancelActivityHandlerTests
{
    [Fact]
    public async Task CancelActivity_ShouldSetStatusToCancelled()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new CancelActivityHandler(db, currentUser);

        var result = await handler.Handle(
            new CancelActivityCommand(seed.CallActivity.Id), CancellationToken.None);

        result.Status.Should().Be(ActivityStatus.Cancelled);
    }
}

public class GetActivitiesQueryHandlerTests
{
    [Fact]
    public async Task GetActivities_AsAdmin_ShouldReturnAll()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetActivitiesHandler(db, currentUser);

        var result = await handler.Handle(new GetActivitiesQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetActivities_AsSales_ShouldReturnOnlyOwn()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep1 owns: CallActivity, MeetingActivity = 2
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetActivitiesHandler(db, currentUser);

        var result = await handler.Handle(new GetActivitiesQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(a => a.AssignedToId.Should().Be(seed.SalesRep1.Id));
    }

    [Fact]
    public async Task GetActivities_FilterByStatus_ShouldReturnOnlyMatching()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetActivitiesHandler(db, currentUser);

        var result = await handler.Handle(
            new GetActivitiesQuery(Status: ActivityStatus.Completed), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.Id.Should().Be(seed.CompletedCall.Id);
    }

    [Fact]
    public async Task GetActivities_FilterByContact_ShouldReturnOnlyMatching()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetActivitiesHandler(db, currentUser);

        var result = await handler.Handle(
            new GetActivitiesQuery(ContactId: seed.AliceJohnson.Id), CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.ContactId.Should().Be(seed.AliceJohnson.Id);
    }

    [Fact]
    public async Task GetActivities_OnlyOverdue_ShouldReturnOnlyPastDue()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetActivitiesHandler(db, currentUser);

        var result = await handler.Handle(
            new GetActivitiesQuery(OnlyOverdue: true), CancellationToken.None);

        // Only OverdueTask has DueDate in the past and is Pending
        result.Items.Should().ContainSingle()
            .Which.Id.Should().Be(seed.OverdueTask.Id);
    }
}

public class GetOverdueActivitiesHandlerTests
{
    [Fact]
    public async Task GetOverdue_AsAdmin_ShouldReturnAllOverdueActivities()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetOverdueActivitiesHandler(db, currentUser);

        var result = await handler.Handle(new GetOverdueActivitiesQuery(), CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Id.Should().Be(seed.OverdueTask.Id);
    }

    [Fact]
    public async Task GetOverdue_AsSales_ShouldOnlyReturnOwnOverdue()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        // SalesRep1 has no overdue activities; SalesRep2 has OverdueTask
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetOverdueActivitiesHandler(db, currentUser);

        var result = await handler.Handle(new GetOverdueActivitiesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}

public class UpdateActivityHandlerTests
{
    [Fact]
    public async Task UpdateActivity_ShouldUpdateFieldsAndClearReminder()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new UpdateActivityHandler(db, currentUser);

        var newDue = DateTime.UtcNow.AddDays(5);
        var result = await handler.Handle(new UpdateActivityCommand(
            seed.CallActivity.Id,
            "Updated Call Title",
            "Updated description.",
            newDue, 45, null), CancellationToken.None);

        result.Title.Should().Be("Updated Call Title");
        result.Description.Should().Be("Updated description.");
        result.DurationMinutes.Should().Be(45);
        result.HasReminder.Should().BeFalse(); // reminder cleared because ReminderAt was null
    }
}
