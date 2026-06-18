using CRM.Domain.Common;
using CRM.Domain.ValueObjects;
namespace CRM.Domain.Entities;

public class Contact : BaseEntity
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Company { get; private set; }
    public Address? Address { get; private set; }
    public string? Notes { get; private set; }
    public string? AvatarUrl { get; private set; }
    public ICollection<string> Tags { get; private set; } = new List<string>();
    public Guid? AssignedToId { get; private set; }

    // Navigation properties
    public User? AssignedTo { get; private set; }
    public ICollection<Deal> Deals { get; private set; } = new List<Deal>();
    public ICollection<Activity> Activities { get; private set; } = new List<Activity>();

    private Contact() { } // EF Core constructor

    private Contact(string firstName, string lastName, string email, string phone, string? company, Guid createdBy)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email.ToLowerInvariant();
        Phone = phone;
        Company = company;
        CreatedBy = createdBy;
    }

    public static Contact Create(string firstName, string lastName, string email, string phone, string? company, Guid createdBy)
    {
        return new Contact(firstName, lastName, email, phone, company, createdBy);
    }

    public void Update(string firstName, string lastName, string email, string phone, string? company)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email.ToLowerInvariant();
        Phone = phone;
        Company = company;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotes(string notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignTo(Guid userId)
    {
        AssignedToId = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTags(params string[] tags)
    {
        foreach (var tag in tags)
        {
            if (!Tags.Contains(tag))
                Tags.Add(tag);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveTag(string tag)
    {
        Tags.Remove(tag);
        UpdatedAt = DateTime.UtcNow;
    }
}