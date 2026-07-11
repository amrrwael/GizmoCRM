using CRM.Application.Common.Exceptions;
using CRM.Application.Features.Users.Commands;
using CRM.Application.Features.Users.Queries;
using CRM.Domain.Enums;
using CRM.Tests.Common;
using FluentAssertions;
using Xunit;

namespace CRM.Tests.Application.Users;

public class UpdateUserProfileHandlerTests
{
    [Fact]
    public async Task UpdateProfile_OwnProfile_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new UpdateUserProfileHandler(db, currentUser);

        var result = await handler.Handle(
            new UpdateUserProfileCommand(seed.SalesRep1.Id, "Olivia-Updated", "Parker-New"),
            CancellationToken.None);

        result.FirstName.Should().Be("Olivia-Updated");
        result.LastName.Should().Be("Parker-New");
    }

    [Fact]
    public async Task UpdateProfile_AdminUpdatingOtherUser_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new UpdateUserProfileHandler(db, currentUser);

        var result = await handler.Handle(
            new UpdateUserProfileCommand(seed.SalesRep1.Id, "Updated", "Name"),
            CancellationToken.None);

        result.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateProfile_SalesUpdatingOtherUser_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new UpdateUserProfileHandler(db, currentUser);

        var act = () => handler.Handle(
            new UpdateUserProfileCommand(seed.SalesRep2.Id, "Hacker", "Attempt"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateProfile_NonExistentUser_ShouldThrowNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var currentUser = MockCurrentUser.Admin();
        var handler = new UpdateUserProfileHandler(db, currentUser);

        var act = () => handler.Handle(
            new UpdateUserProfileCommand(Guid.NewGuid(), "Ghost", "User"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class ChangeUserRoleHandlerTests
{
    [Fact]
    public async Task ChangeRole_AdminChangingOtherUser_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new ChangeUserRoleHandler(db, currentUser);

        var result = await handler.Handle(
            new ChangeUserRoleCommand(seed.SalesRep1.Id, UserRole.Manager),
            CancellationToken.None);

        result.Role.Should().Be(UserRole.Manager);
    }

    [Fact]
    public async Task ChangeRole_AdminChangingOwnRole_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new ChangeUserRoleHandler(db, currentUser);

        var act = () => handler.Handle(
            new ChangeUserRoleCommand(seed.Admin.Id, UserRole.Sales),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*cannot change your own role*");
    }

    [Fact]
    public async Task ChangeRole_NonAdmin_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Manager(seed.Manager.Id);
        var handler = new ChangeUserRoleHandler(db, currentUser);

        var act = () => handler.Handle(
            new ChangeUserRoleCommand(seed.SalesRep1.Id, UserRole.Admin),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only admins*");
    }
}

public class DeactivateUserHandlerTests
{
    [Fact]
    public async Task DeactivateUser_AsAdmin_ShouldDeactivate()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new DeactivateUserHandler(db, currentUser);

        await handler.Handle(new DeactivateUserCommand(seed.SalesRep1.Id), CancellationToken.None);

        var user = await db.Users.FindAsync(seed.SalesRep1.Id);
        user!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateUser_AdminDeactivatingSelf_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new DeactivateUserHandler(db, currentUser);

        var act = () => handler.Handle(
            new DeactivateUserCommand(seed.Admin.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*deactivate your own account*");
    }

    [Fact]
    public async Task DeactivateUser_NonAdmin_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Manager(seed.Manager.Id);
        var handler = new DeactivateUserHandler(db, currentUser);

        var act = () => handler.Handle(
            new DeactivateUserCommand(seed.SalesRep1.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}

public class GetAllUsersHandlerTests
{
    [Fact]
    public async Task GetAllUsers_AsAdmin_ShouldReturnAllUsers()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetAllUsersHandler(db, currentUser);

        var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        result.Should().HaveCount(4); // Admin, Manager, SalesRep1, SalesRep2
    }

    [Fact]
    public async Task GetAllUsers_AsManager_ShouldSucceed()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Manager(seed.Manager.Id);
        var handler = new GetAllUsersHandler(db, currentUser);

        var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        result.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllUsers_AsSales_ShouldThrowForbidden()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Sales(seed.SalesRep1.Id);
        var handler = new GetAllUsersHandler(db, currentUser);

        var act = () => handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Sales users cannot view*");
    }

    [Fact]
    public async Task GetAllUsers_ShouldBeOrderedByName()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new GetAllUsersHandler(db, currentUser);

        var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        var names = result.Select(u => u.FirstName).ToList();
        names.Should().BeInAscendingOrder();
    }
}
