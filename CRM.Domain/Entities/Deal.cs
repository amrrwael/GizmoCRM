using CRM.Domain.Common;
using CRM.Domain.Enums;

namespace CRM.Domain.Entities;

public class Deal : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public decimal Value { get; private set; }
    public DealStage Stage { get; private set; }
    public int Probability { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ContactId { get; private set; }
    public DateTime? ExpectedCloseDate { get; private set; }
    public string? Description { get; private set; }
    public string? LostReason { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    // Navigation properties
    public User Owner { get; private set; } = null!;
    public Contact Contact { get; private set; } = null!;
    public ICollection<Activity> Activities { get; private set; } = new List<Activity>();

    private Deal() { }

    private Deal(string title, decimal value, Guid ownerId, Guid contactId, DateTime? expectedCloseDate, string? description, Guid createdBy)
    {
        Title = title.Trim();
        Value = value;
        Stage = DealStage.Lead;
        Probability = 10;
        OwnerId = ownerId;
        ContactId = contactId;
        ExpectedCloseDate = expectedCloseDate;
        Description = description;
        CreatedBy = createdBy;
    }

    public static Deal Create(string title, decimal value, Guid ownerId, Guid contactId, DateTime? expectedCloseDate, string? description, Guid createdBy)
        => new(title, value, ownerId, contactId, expectedCloseDate, description, createdBy);

    public void UpdateDetails(string title, decimal value, DateTime? expectedCloseDate, string? description)
    {
        Title = title.Trim();
        Value = value;
        ExpectedCloseDate = expectedCloseDate;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveToStage(DealStage newStage, string? lostReason = null)
    {
        Stage = newStage;
        UpdatedAt = DateTime.UtcNow;

        Probability = newStage switch
        {
            DealStage.Lead => 10,
            DealStage.Qualified => 30,
            DealStage.Proposal => 60,
            DealStage.Negotiation => 80,
            DealStage.Won => 100,
            DealStage.Lost => 0,
            _ => Probability
        };

        if (newStage is DealStage.Won or DealStage.Lost)
            ClosedAt = DateTime.UtcNow;

        if (newStage == DealStage.Lost)
            LostReason = lostReason;
    }

    public void Reassign(Guid newOwnerId)
    {
        OwnerId = newOwnerId;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsOpen => Stage is not (DealStage.Won or DealStage.Lost);
}