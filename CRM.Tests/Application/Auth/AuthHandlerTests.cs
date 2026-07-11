using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Application.Features.Auth.Commands;
using CRM.Domain.Entities;
using CRM.Domain.Enums;
using CRM.Tests.Common;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CRM.Tests.Application.Auth;

public class LoginCommandHandlerTests
{
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();

    public LoginCommandHandlerTests()
    {
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("refresh-token");
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturnAuthResponse()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var handler = new LoginCommandHandler(db, _tokenService);

        var result = await handler.Handle(
            new LoginCommand("olivia.parker@gizmocrm.com", "Sales@1234"),
            CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.User.Email.Should().Be("olivia.parker@gizmocrm.com");
        result.User.Role.Should().Be(UserRole.Sales);
    }

    [Fact]
    public async Task Login_WrongPassword_ShouldThrowUnauthorized()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedData.CreateAsync(db);
        var handler = new LoginCommandHandler(db, _tokenService);

        var act = () => handler.Handle(
            new LoginCommand("olivia.parker@gizmocrm.com", "WrongPassword!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task Login_NonExistentEmail_ShouldThrowUnauthorized()
    {
        await using var db = TestDbContextFactory.Create();
        var handler = new LoginCommandHandler(db, _tokenService);

        var act = () => handler.Handle(
            new LoginCommand("nobody@nowhere.com", "Password@1"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Login_DeactivatedUser_ShouldThrowUnauthorized()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        seed.SalesRep1.Deactivate();
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(db, _tokenService);
        var act = () => handler.Handle(
            new LoginCommand("olivia.parker@gizmocrm.com", "Sales@1234"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task Login_ShouldRecordLastLoginAt()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var handler = new LoginCommandHandler(db, _tokenService);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await handler.Handle(
            new LoginCommand("olivia.parker@gizmocrm.com", "Sales@1234"),
            CancellationToken.None);

        var user = await db.Users.FindAsync(seed.SalesRep1.Id);
        user!.LastLoginAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedData.CreateAsync(db);
        var handler = new LoginCommandHandler(db, _tokenService);

        var result = await handler.Handle(
            new LoginCommand("OLIVIA.PARKER@GIZMOCRM.COM", "Sales@1234"),
            CancellationToken.None);

        result.Should().NotBeNull();
    }
}

public class RegisterCommandHandlerTests
{
    [Fact]
    public async Task Register_NewUser_ShouldCreateAndReturnUserDto()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new RegisterCommandHandler(db, currentUser);

        var result = await handler.Handle(
            new RegisterCommand("newuser@acme.com", "NewPass@123", "New", "User", UserRole.Sales),
            CancellationToken.None);

        result.Email.Should().Be("newuser@acme.com");
        result.FirstName.Should().Be("New");
        result.Role.Should().Be(UserRole.Sales);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldThrowConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new RegisterCommandHandler(db, currentUser);

        var act = () => handler.Handle(
            new RegisterCommand("olivia.parker@gizmocrm.com", "NewPass@123", "Olivia", "Clone", UserRole.Sales),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*olivia.parker@gizmocrm.com*");
    }

    [Fact]
    public async Task Register_ShouldHashPassword()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new RegisterCommandHandler(db, currentUser);

        await handler.Handle(
            new RegisterCommand("secure@test.com", "Secure@123", "Test", "User", UserRole.Sales),
            CancellationToken.None);

        var user = db.Users.Single(u => u.Email == "secure@test.com");
        user.PasswordHash.Should().NotBe("Secure@123"); // must not be plain text
        BCrypt.Net.BCrypt.Verify("Secure@123", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Register_DuplicateEmail_CaseInsensitive_ShouldThrowConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);
        var currentUser = MockCurrentUser.Admin(seed.Admin.Id);
        var handler = new RegisterCommandHandler(db, currentUser);

        // Try to register with uppercase version of existing email
        var act = () => handler.Handle(
            new RegisterCommand("OLIVIA.PARKER@GIZMOCRM.COM", "NewPass@123", "Clone", "User", UserRole.Sales),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}

public class RefreshTokenHandlerTests
{
    [Fact]
    public async Task Refresh_ValidToken_ShouldReturnNewAuthResponse()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        seed.SalesRep1.SetRefreshToken("valid-refresh-token", DateTime.UtcNow.AddDays(7));
        await db.SaveChangesAsync();

        var tokenService = Substitute.For<ITokenService>();
        tokenService.GetUserIdFromExpiredToken("old-access-token").Returns(seed.SalesRep1.Id);
        tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("new-access-token");
        tokenService.GenerateRefreshToken().Returns("new-refresh-token");

        var handler = new RefreshTokenCommandHandler(db, tokenService);
        var result = await handler.Handle(
            new RefreshTokenCommand("old-access-token", "valid-refresh-token"),
            CancellationToken.None);

        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task Refresh_ExpiredRefreshToken_ShouldThrowUnauthorized()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        seed.SalesRep1.SetRefreshToken("old-token", DateTime.UtcNow.AddDays(-1)); // expired
        await db.SaveChangesAsync();

        var tokenService = Substitute.For<ITokenService>();
        tokenService.GetUserIdFromExpiredToken(Arg.Any<string>()).Returns(seed.SalesRep1.Id);

        var handler = new RefreshTokenCommandHandler(db, tokenService);
        var act = () => handler.Handle(
            new RefreshTokenCommand("access", "old-token"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Refresh_WrongRefreshToken_ShouldThrowUnauthorized()
    {
        await using var db = TestDbContextFactory.Create();
        var seed = await SeedData.CreateAsync(db);

        seed.SalesRep1.SetRefreshToken("correct-token", DateTime.UtcNow.AddDays(7));
        await db.SaveChangesAsync();

        var tokenService = Substitute.For<ITokenService>();
        tokenService.GetUserIdFromExpiredToken(Arg.Any<string>()).Returns(seed.SalesRep1.Id);

        var handler = new RefreshTokenCommandHandler(db, tokenService);
        var act = () => handler.Handle(
            new RefreshTokenCommand("access", "wrong-token"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
