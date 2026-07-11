using CRM.Domain.Entities;

namespace CRM.Application.Common.Interfaces;

/// <summary>
/// Talks to the Gmail REST API directly over HttpClient (no Google SDK dependency,
/// keeping the dependency graph small and easy to audit). All OAuth credentials
/// (Client ID / Client Secret) are read from IIntegrationSettingsService, i.e. from
/// the Settings -> Integrations -> Gmail screen — never from a config file.
/// </summary>
public interface IGmailService
{
    /// <summary>Builds the Google consent screen URL the frontend redirects the user to.</summary>
    Task<string> GetAuthorizationUrlAsync(Guid userId, string redirectUri, CancellationToken ct = default);

    /// <summary>Exchanges the OAuth "code" for tokens and stores/updates the GmailAccount.</summary>
    Task<GmailAccount> HandleOAuthCallbackAsync(string code, Guid userId, string redirectUri, CancellationToken ct = default);

    Task<List<GmailAccount>> GetAccountsAsync(Guid userId, CancellationToken ct = default);

    Task DisconnectAsync(Guid accountId, Guid userId, CancellationToken ct = default);

    Task<EmailMessage> SendEmailAsync(
        Guid accountId, Guid requestingUserId, string to, string subject, string bodyText,
        Guid? contactId = null, string? threadId = null, CancellationToken ct = default);

    /// <summary>Pulls recent messages from Gmail and upserts them into EmailMessages.</summary>
    Task<List<EmailMessage>> SyncInboxAsync(Guid accountId, Guid requestingUserId, int maxResults = 25, CancellationToken ct = default);

    Task<List<EmailMessage>> GetStoredMessagesAsync(Guid userId, Guid? contactId = null, int page = 1, int pageSize = 30, CancellationToken ct = default);

    Task<EmailMessage?> GetMessageAsync(Guid emailMessageId, Guid userId, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid emailMessageId, Guid userId, CancellationToken ct = default);
}
