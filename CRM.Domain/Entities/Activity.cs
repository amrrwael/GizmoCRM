using CRM.Domain.Common;
using CRM.Domain.Enums;

namespace CRM.Domain.Entities;

public class Activity : BaseEntity
{
    public ActivityType Type { get; private set; }
    public ActivityStatus Status { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid AssignedToId { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? DealId { get; private set; }
    public bool HasReminder { get; private set; }
    public DateTime? ReminderAt { get; private set; }

    // Navigation properties
    public User AssignedTo { get; private set; } = null!;
    public Contact? Contact { get; private set; }
    public Deal? Deal { get; private set; }

    private Activity() { } // EF Core constructor

    private Activity(ActivityType type, string title, string? description, DateTime? dueDate, Guid assignedToId, Guid? contactId, Guid? dealId)
    {
        Type = type;
        Status = ActivityStatus.Pending;
        Title = title;
        Description = description;
        DueDate = dueDate;
        AssignedToId = assignedToId;
        ContactId = contactId;
        DealId = dealId;
    }

    public static Activity Create(ActivityType type, string title, string? description, DateTime? dueDate, Guid assignedToId, Guid? contactId, Guid? dealId)
    {
        return new Activity(type, title, description, dueDate, assignedToId, contactId, dealId);
    }

    public void Complete()
    {
        Status = ActivityStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = ActivityStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetReminder(DateTime reminderAt)
    {
        HasReminder = true;
        ReminderAt = reminderAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveReminder()
    {
        HasReminder = false;
        ReminderAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}