using CRM.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CRM.Application.Features.Auth.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt);

// ── Login ──────────────────────────────────────────────────────────────────────

public record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// ── Register (Admin only) ──────────────────────────────────────────────────────

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserRole Role) : IRequest<UserDto>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum();
    }
}

// ── Refresh Token ──────────────────────────────────────────────────────────────

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<AuthResponse>;

// ── Revoke Token (Logout) ──────────────────────────────────────────────────────

public record RevokeTokenCommand : IRequest;