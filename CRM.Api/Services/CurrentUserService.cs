using CRM.Application.Common.Interfaces;
using CRM.Domain.Enums;
using System.Security.Claims;

namespace CRM.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub"), out var id)
            ? id : Guid.Empty;

    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public UserRole Role
    {
        get
        {
            var role = User?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(role, out var parsed) ? parsed : UserRole.Sales;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}