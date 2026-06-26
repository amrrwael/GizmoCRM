using CRM.Domain.Common;

namespace CRM.Domain.Entities;

public class Contact : BaseEntity
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Company { get; private set; }
    public string? Position { get; private set; }
    public string? Notes { get; private set; }
    public string? AvatarUrl { get; private set; }
    public Guid? AssignedToId { get; private set; }

    // Stored as comma-separated in DB, exposed as list
    private string _tags = string.Empty;
    public IReadOnlyList<string> Tags =>
        string.IsNullOrWhiteSpace(_tags)
            ? Array.Empty<string>()
            : _tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    // Navigation properties
    public User? AssignedTo { get; private set; }
    public ICollection<Deal> Deals { get; private set; } = new List<Deal>();
    public ICollection<Activity> Activities { get; private set; } = new List<Activity>();

    private Contact() { }

    private Contact(string firstName, string lastName, string email, string? phone, string? company, string? position, Guid createdBy)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.ToLowerInvariant().Trim();
        Phone = phone?.Trim();
        Company = company?.Trim();
        Position = position?.Trim();
        CreatedBy = createdBy;
    }

    public static Contact Create(string firstName, string lastName, string email, string? phone, string? company, string? position, Guid createdBy)
        => new(firstName, lastName, email, phone, company, position, createdBy);

    public string FullName => $"{FirstName} {LastName}";

    public void Update(string firstName, string lastName, string email, string? phone, string? company, string? position)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.ToLowerInvariant().Trim();
        Phone = phone?.Trim();
        Company = company?.Trim();
        Position = position?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignTo(Guid? userId)
    {
        AssignedToId = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTags(IEnumerable<string> tags)
    {
        _tags = string.Join(',', tags.Select(t => t.Trim().ToLowerInvariant()).Distinct());
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTag(string tag)
    {
        var current = Tags.ToHashSet();
        current.Add(tag.Trim().ToLowerInvariant());
        _tags = string.Join(',', current);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveTag(string tag)
    {
        var current = Tags.ToHashSet();
        current.Remove(tag.Trim().ToLowerInvariant());
        _tags = string.Join(',', current);
        UpdatedAt = DateTime.UtcNow;
    }
}