using CRM.Application.Common.Exceptions;
using CRM.Application.Common.Interfaces;
using CRM.Application.Features.Auth.Commands;
using CRM.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application.Features.Users.Queries;

public record GetAllUsersQuery : IRequest<List<UserDto>>;

public record GetUserByIdQuery(Guid UserId) : IRequest<UserDto>;

public record GetCurrentUserQuery : IRequest<UserDto>;

public class GetAllUsersHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetAllUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Role == UserRole.Sales)
            throw new ForbiddenException("Sales users cannot view the user list.");

        return await db.Users
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new UserDto(u.Id, u.Email, u.FirstName, u.LastName,
                u.FirstName + " " + u.LastName, u.Role, u.IsActive, u.LastLoginAt))
            .ToListAsync(cancellationToken);
    }
}

public class GetUserByIdHandler(IApplicationDbContext db)
    : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([request.UserId], cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.FullName, user.Role, user.IsActive, user.LastLoginAt);
    }
}

public class GetCurrentUserHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([currentUser.UserId], cancellationToken)
            ?? throw new NotFoundException("User", currentUser.UserId);

        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.FullName, user.Role, user.IsActive, user.LastLoginAt);
    }
}