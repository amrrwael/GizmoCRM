using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Auth.Commands;

public class LoginCommandHandler(IApplicationDbContext db, ITokenService tokenService)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedException("Your account has been deactivated. Please contact an administrator.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
        user.RecordLogin();

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken, refreshToken, expiresAt, user.ToDto());
    }
}

public class RegisterCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RegisterCommand, UserDto>
{
    public async Task<UserDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken))
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Create(request.Email, passwordHash, request.FirstName, request.LastName, request.Role);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user.ToDto();
    }
}

public class RefreshTokenCommandHandler(IApplicationDbContext db, ITokenService tokenService)
    : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = tokenService.GetUserIdFromExpiredToken(request.AccessToken)
            ?? throw new UnauthorizedException("Invalid access token.");

        var user = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new UnauthorizedException("User not found.");

        if (user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime < DateTime.UtcNow)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken, refreshToken, expiresAt, user.ToDto());
    }
}

public class RevokeTokenCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RevokeTokenCommand>
{
    public async Task Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([currentUser.UserId], cancellationToken);
        if (user is null) return;

        user.SetRefreshToken(null, null);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// Extension to map User → UserDto
internal static class UserMappingExtensions
{
    public static UserDto ToDto(this Domain.Entities.User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.FullName, user.Role, user.IsActive, user.LastLoginAt);
}