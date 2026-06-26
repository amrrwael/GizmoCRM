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
    public string? Outcome { get; private set; }
    public int? DurationMinutes { get; private set; }

    // Navigation properties
    public User AssignedTo { get; private set; } = null!;
    public Contact? Contact { get; private set; }
    public Deal? Deal { get; private set; }

    private Activity() { }

    private Activity(ActivityType type, string title, string? description, DateTime? dueDate,
        Guid assignedToId, Guid? contactId, Guid? dealId, Guid createdBy)
    {
        Type = type;
        Status = ActivityStatus.Pending;
        Title = title.Trim();
        Description = description;
        DueDate = dueDate;
        AssignedToId = assignedToId;
        ContactId = contactId;
        DealId = dealId;
        CreatedBy = createdBy;
    }

    public static Activity Create(ActivityType type, string title, string? description, DateTime? dueDate,
        Guid assignedToId, Guid? contactId, Guid? dealId, Guid createdBy)
        => new(type, title, description, dueDate, assignedToId, contactId, dealId, createdBy);

    public void Update(string title, string? description, DateTime? dueDate, int? durationMinutes)
    {
        Title = title.Trim();
        Description = description;
        DueDate = dueDate;
        DurationMinutes = durationMinutes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(string? outcome = null)
    {
        Status = ActivityStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Outcome = outcome;
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

    public void ClearReminder()
    {
        HasReminder = false;
        ReminderAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsOverdue => Status == ActivityStatus.Pending && DueDate.HasValue && DueDate < DateTime.UtcNow;
}