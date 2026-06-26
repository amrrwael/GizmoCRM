using CRM.Domain.Common;
using CRM.Domain.Enums;

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
    public ICollection<Deal> OwnedDeals { get; private set; } = new List<Deal>();
    public ICollection<Activity> Activities { get; private set; } = new List<Activity>();
    public ICollection<Contact> AssignedContacts { get; private set; } = new List<Contact>();

    private User() { } // EF Core constructor

    private User(string email, string passwordHash, string firstName, string lastName, UserRole role)
    {
        Email = email.ToLowerInvariant().Trim();
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Role = role;
        IsActive = true;
    }

    public static User Create(string email, string passwordHash, string firstName, string lastName, UserRole role)
        => new(email, passwordHash, firstName, lastName, role);

    public string FullName => $"{FirstName} {LastName}";

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRefreshToken(string? refreshToken, DateTime? expiryTime)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryTime;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}