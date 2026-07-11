namespace CRM.Application.Common.Interfaces;

/// <summary>
/// Every third-party credential (Telegram bot token, Gmail OAuth client id/secret,
/// Twilio SID/auth token/phone number) is stored here, encrypted at rest, and is
/// configured entirely from the Settings -> Integrations page in the app.
/// Nobody needs to touch appsettings.json, a .env file, or any source file.
/// </summary>
public interface IIntegrationSettingsService
{
    /// <summary>Returns the decrypted value, or null if not configured.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Encrypts (if isSecret) and upserts a setting.</summary>
    Task SetAsync(string key, string value, string category, bool isSecret = true, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns all keys in a category with secret values masked (e.g. "sk_l...ab12")
    /// so the frontend can show "configured" state without ever exposing the raw secret.
    /// </summary>
    Task<Dictionary<string, string?>> GetMaskedForCategoryAsync(string category, CancellationToken ct = default);

    Task<bool> IsCategoryConfiguredAsync(string category, IEnumerable<string> requiredKeys, CancellationToken ct = default);
}
