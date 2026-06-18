using CRM.Domain.Common;
using CRM.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Domain.Entities;

public class Deal : BaseEntity
{
    public string Title { get; private set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; private set; }

    public DealStage Stage { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ContactId { get; private set; }
    public DateTime? ExpectedCloseDate { get; private set; }
    public string? Description { get; private set; }
    public int Probability { get; private set; }
    public string? LostReason { get; private set; }

    // Navigation properties
    public User Owner { get; private set; } = null!;
    public Contact Contact { get; private set; } = null!;
    public ICollection<Activity> Activities { get; private set; } = new List<Activity>();

    private Deal() { } // EF Core constructor

    private Deal(string title, decimal value, Guid ownerId, Guid contactId, DateTime? expectedCloseDate, string? description)
    {
        Title = title;
        Value = value;
        Stage = DealStage.Lead;
        OwnerId = ownerId;
        ContactId = contactId;
        ExpectedCloseDate = expectedCloseDate;
        Description = description;
        Probability = 10;
    }

    public static Deal Create(string title, decimal value, Guid ownerId, Guid contactId, DateTime? expectedCloseDate, string? description)
    {
        return new Deal(title, value, ownerId, contactId, expectedCloseDate, description);
    }

    public void UpdateStage(DealStage newStage, string? lostReason = null)
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

        if (newStage == DealStage.Lost)
            LostReason = lostReason;
    }

    public void UpdateDetails(string title, decimal value, DateTime? expectedCloseDate, string? description)
    {
        Title = title;
        Value = value;
        ExpectedCloseDate = expectedCloseDate;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reassign(Guid newOwnerId)
    {
        OwnerId = newOwnerId;
        UpdatedAt = DateTime.UtcNow;
    }
}