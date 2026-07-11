// CRM.Domain/Entities/TelegramMessage.cs
using CRM.Domain.Common;
namespace CRM.Domain.Entities;

public class TelegramMessage : BaseEntity
{
    // Nullable for the same reason as TelegramChat.ContactId: a message can arrive
    // from a chat that isn't linked to a CRM contact yet.
    public Guid? ContactId { get; set; }
    public virtual Contact? Contact { get; set; }

    // FK to the chat this message belongs to (the old model had no relationship
    // between TelegramMessage and TelegramChat at all — messages could only ever
    // be looked up by ContactId, which broke as soon as ContactId was null).
    public Guid TelegramChatId { get; set; }
    public virtual TelegramChat Chat { get; set; } = null!;

    public string? TelegramMessageId { get; set; } // Telegram's message ID
    public string Content { get; set; } = string.Empty;
    public bool IsFromContact { get; set; } // true = contact sent, false = we sent
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? MediaType { get; set; } // photo, document, etc.
    public string? MediaUrl { get; set; }
}
