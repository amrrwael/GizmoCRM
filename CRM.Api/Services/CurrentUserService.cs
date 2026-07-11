// CRM.Api/Services/CurrentUserService.cs
using CRM.Application.Common.Interfaces;
using CRM.Domain.Enums;
using System.Security.Claims;

namespace CRM.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            if (!IsAuthenticated)
                return Guid.Empty;

            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub")
                ?? User?.FindFirstValue("userId");

            return Guid.TryParse(userId, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => IsAuthenticated
        ? User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty
        : string.Empty;

    public UserRole Role
    {
        get
        {
            if (!IsAuthenticated)
                return UserRole.Sales; // Default role for unauthenticated

            var role = User?.FindFirstValue(ClaimTypes.Role)
                ?? User?.FindFirstValue("role");

            return Enum.TryParse<UserRole>(role, true, out var parsed) ? parsed : UserRole.Sales;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}