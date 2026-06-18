using CRM.Domain.Common;
using CRM.Domain.Enums;
using System.Diagnostics;

namespace CRM.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }

    // Navigation properties
    public ICollection<Deal> Deals { get; private set; } = new List<Deal>();
    public ICollection<Activity> Activities { get; private set; } = new List<Activity>();

    private User() { } // EF Core constructor

    private User(string email, string passwordHash, string firstName, string lastName, UserRole role)
    {
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        IsActive = true;
    }

    public static User Create(string email, string passwordHash, string firstName, string lastName, UserRole role)
    {
        return new User(email, passwordHash, firstName, lastName, role);
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRefreshToken(string? refreshToken, DateTime? expiryTime)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryTime;
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}