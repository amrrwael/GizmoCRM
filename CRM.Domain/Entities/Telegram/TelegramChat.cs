using CRM.Domain.Common;

namespace CRM.Domain.Entities;

public class TelegramChat : BaseEntity
{
    // Nullable: an inbound message can create a chat before any CRM user has
    // linked it to a Contact record. The previous version required a non-null
    // ContactId, which made it impossible to ever save a brand-new incoming chat
    // (and crashed EF's non-null FK constraint the moment a stranger messaged the bot).
    public Guid? ContactId { get; set; }
    public virtual Contact? Contact { get; set; }

    public string? TelegramChatId { get; set; } // Telegram's chat ID
    public string? TelegramUserId { get; set; } // Telegram user ID
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }

    public virtual ICollection<TelegramMessage> Messages { get; set; } = new List<TelegramMessage>();
}
