using CRM.Domain.Common;

namespace CRM.Domain.Entities;

/// <summary>
/// Generic, admin-editable key/value store for third-party integration credentials
/// (Telegram bot token, Gmail OAuth client id/secret, Twilio SID/token, etc).
/// This exists so nobody ever has to open appsettings.json or any source file to
/// configure an integration — everything is entered once through the Settings ->
/// Integrations screen in the app and saved here. Secret values are encrypted at
/// rest by IIntegrationSettingsService before they ever reach the database.
/// </summary>
public class IntegrationSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;      // e.g. "Telegram:BotToken"
    public string? Value { get; set; }                    // encrypted ciphertext, never plaintext
    public bool IsSecret { get; set; } = true;
    public string Category { get; set; } = string.Empty;  // "Telegram" | "Gmail" | "Twilio"
}
