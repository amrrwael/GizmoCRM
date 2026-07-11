using CRM.Domain.Common;

namespace CRM.Domain.Entities;

public class EmailMessage : BaseEntity
{
    public Guid GmailAccountId { get; set; }
    public virtual GmailAccount GmailAccount { get; set; } = null!;

    // Optional link to a CRM contact, matched by email address when possible.
    public Guid? ContactId { get; set; }
    public virtual Contact? Contact { get; set; }

    public string? GmailMessageId { get; set; }
    public string? GmailThreadId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public bool IsFromContact { get; set; } // true = received, false = sent by CRM user
    public bool IsRead { get; set; }
    public bool IsDraft { get; set; }
    public DateTime ReceivedAt { get; set; }
}
