using CRM.Domain.Common;

namespace CRM.Domain.Entities;

/// <summary>
/// A Gmail mailbox connected by a CRM user via the in-app "Connect Gmail" OAuth
/// button on the Integrations page. Tokens are encrypted at rest.
/// </summary>
public class GmailAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string EmailAddress { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public string? HistoryId { get; set; } // Gmail history id, used for incremental sync

    public virtual ICollection<EmailMessage> Messages { get; set; } = new List<EmailMessage>();
}
