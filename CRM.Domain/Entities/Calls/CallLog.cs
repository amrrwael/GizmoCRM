using CRM.Domain.Common;
using CRM.Domain.Enums;

namespace CRM.Domain.Entities;

public class CallLog : BaseEntity
{
    public Guid InitiatedByUserId { get; set; }
    public virtual User InitiatedByUser { get; set; } = null!;

    public Guid? ContactId { get; set; }
    public virtual Contact? Contact { get; set; }

    public string FromNumber { get; set; } = string.Empty;
    public string ToNumber { get; set; } = string.Empty;
    public CallDirection Direction { get; set; }
    public CallStatus Status { get; set; }
    public string? ProviderCallSid { get; set; } // Twilio Call SID
    public int DurationSeconds { get; set; }
    public string? RecordingUrl { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
