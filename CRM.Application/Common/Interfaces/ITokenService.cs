using CRM.Domain.Entities;
using System.Security.Claims;

namespace CRM.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? GetPrincipalFromExpiredToken(string token);
}