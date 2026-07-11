namespace CRM.Domain.Common;

public abstract class BaseEntity
{
    // NOTE: these were `protected set`, which made it impossible for services outside the
    // Domain project (e.g. CRM.Infrastructure.Services.TelegramService) to construct entities
    // with object initializers such as `new TelegramChat { Id = ..., CreatedAt = ... }`.
    // That produced CS0272 "inaccessible set accessor" compile errors. Widening to `public set`
    // is backwards compatible (every existing private-ctor/static-factory entity still compiles)
    // and unblocks the simple POCO-style entities (TelegramChat, TelegramMessage, IntegrationSetting,
    // GmailAccount, EmailMessage, CallLog) that don't use the rich-domain-model pattern.
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}