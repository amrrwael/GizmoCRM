using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Application.Features.Auth.Commands;
using CRM.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Users.Commands;

// ── Commands ───────────────────────────────────────────────────────────────────

public record UpdateUserProfileCommand(Guid UserId, string FirstName, string LastName) : IRequest<UserDto>;

public class UpdateUserProfileValidator : AbstractValidator<UpdateUserProfileCommand>
{
	public UpdateUserProfileValidator()
	{
		RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
		RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
	}
}

public record ChangeUserRoleCommand(Guid UserId, UserRole NewRole) : IRequest<UserDto>;

public record DeactivateUserCommand(Guid UserId) : IRequest;

public record ActivateUserCommand(Guid UserId) : IRequest;

// ── Handlers ───────────────────────────────────────────────────────────────────

public class UpdateUserProfileHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<UpdateUserProfileCommand, UserDto>
{
	public async Task<UserDto> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
	{
		// Users can update their own profile; Admins can update anyone's
		if (currentUser.UserId != request.UserId && currentUser.Role != UserRole.Admin)
			throw new ForbiddenException();

		var user = await db.Users.FindAsync([request.UserId], cancellationToken)
			?? throw new NotFoundException("User", request.UserId);

		user.UpdateProfile(request.FirstName, request.LastName);
		await db.SaveChangesAsync(cancellationToken);

		return new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.FullName, user.Role, user.IsActive, user.LastLoginAt);
	}
}

public class ChangeUserRoleHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<ChangeUserRoleCommand, UserDto>
{
	public async Task<UserDto> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
	{
		if (currentUser.Role != UserRole.Admin)
			throw new ForbiddenException("Only admins can change user roles.");

		var user = await db.Users.FindAsync([request.UserId], cancellationToken)
			?? throw new NotFoundException("User", request.UserId);

		if (user.Id == currentUser.UserId)
			throw new ForbiddenException("You cannot change your own role.");

		user.ChangeRole(request.NewRole);
		await db.SaveChangesAsync(cancellationToken);

		return new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.FullName, user.Role, user.IsActive, user.LastLoginAt);
	}
}

public class DeactivateUserHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<DeactivateUserCommand>
{
	public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
	{
		if (currentUser.Role != UserRole.Admin)
			throw new ForbiddenException("Only admins can deactivate users.");

		var user = await db.Users.FindAsync([request.UserId], cancellationToken)
			?? throw new NotFoundException("User", request.UserId);

		if (user.Id == currentUser.UserId)
			throw new ForbiddenException("You cannot deactivate your own account.");

		user.Deactivate();
		await db.SaveChangesAsync(cancellationToken);
	}
}

public class ActivateUserHandler(IApplicationDbContext db, ICurrentUserService currentUser)
	: IRequestHandler<ActivateUserCommand>
{
	public async Task Handle(ActivateUserCommand request, CancellationToken cancellationToken)
	{
		if (currentUser.Role != UserRole.Admin)
			throw new ForbiddenException("Only admins can activate users.");

		var user = await db.Users.FindAsync([request.UserId], cancellationToken)
			?? throw new NotFoundException("User", request.UserId);

		user.Activate();
		await db.SaveChangesAsync(cancellationToken);
	}
}