using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRM.Infrastructure.Services;

public class GmailService : IGmailService
{
    private const string Scopes = "https://www.googleapis.com/auth/gmail.send " +
                                   "https://www.googleapis.com/auth/gmail.readonly " +
                                   "https://www.googleapis.com/auth/gmail.modify " +
                                   "https://www.googleapis.com/auth/userinfo.email";

    private readonly HttpClient _http;
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationSettingsService _settings;
    private readonly IDataProtector _protector;
    private readonly ILogger<GmailService> _logger;

    public GmailService(
        HttpClient http,
        IApplicationDbContext context,
        IIntegrationSettingsService settings,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<GmailService> logger)
    {
        _http = http;
        _context = context;
        _settings = settings;
        _protector = dataProtectionProvider.CreateProtector("CRM.GmailTokens.v1");
        _logger = logger;
    }

    private async Task<(string clientId, string clientSecret)> GetOAuthCredentialsOrThrowAsync(CancellationToken ct)
    {
        var clientId = await _settings.GetAsync("Gmail:ClientId", ct);
        var clientSecret = await _settings.GetAsync("Gmail:ClientSecret", ct);
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(
                "Gmail isn't configured yet. Go to Settings -> Integrations -> Gmail and enter your Google OAuth Client ID and Client Secret.");
        return (clientId, clientSecret);
    }

    public async Task<string> GetAuthorizationUrlAsync(Guid userId, string redirectUri, CancellationToken ct = default)
    {
        var (clientId, _) = await GetOAuthCredentialsOrThrowAsync(ct);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = userId.ToString(),
        };
        var qs = string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{qs}";
    }

    public async Task<GmailAccount> HandleOAuthCallbackAsync(string code, Guid userId, string redirectUri, CancellationToken ct = default)
    {
        var (clientId, clientSecret) = await GetOAuthCredentialsOrThrowAsync(ct);

        var tokenResponse = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        }), ct);

        tokenResponse.EnsureSuccessStatusCode();
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Google did not return a token response.");

        var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/profile");
        profileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var profileResp = await _http.SendAsync(profileReq, ct);
        profileResp.EnsureSuccessStatusCode();
        var profile = await profileResp.Content.ReadFromJsonAsync<GmailProfileResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Could not read Gmail profile.");

        var account = await _context.GmailAccounts.FirstOrDefaultAsync(a => a.EmailAddress == profile.EmailAddress, ct);

        if (account == null)
        {
            account = new GmailAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EmailAddress = profile.EmailAddress,
                CreatedAt = DateTime.UtcNow,
            };
            _context.GmailAccounts.Add(account);
        }

        account.EncryptedAccessToken = _protector.Protect(tokens.AccessToken);
        // Google only returns a refresh_token the FIRST time consent is granted;
        // keep the previous one if this is a re-auth without a new refresh_token.
        if (!string.IsNullOrEmpty(tokens.RefreshToken))
            account.EncryptedRefreshToken = _protector.Protect(tokens.RefreshToken);
        account.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return account;
    }

    public async Task<List<GmailAccount>> GetAccountsAsync(Guid userId, CancellationToken ct = default)
        => await _context.GmailAccounts.Where(a => a.UserId == userId && a.IsActive).ToListAsync(ct);

    public async Task DisconnectAsync(Guid accountId, Guid userId, CancellationToken ct = default)
    {
        var account = await _context.GmailAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, ct);
        if (account == null) return;
        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    private async Task<string> GetValidAccessTokenAsync(GmailAccount account, CancellationToken ct)
    {
        if (account.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
            return _protector.Unprotect(account.EncryptedAccessToken);

        var (clientId, clientSecret) = await GetOAuthCredentialsOrThrowAsync(ct);
        var refreshToken = _protector.Unprotect(account.EncryptedRefreshToken);

        var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }), ct);
        resp.EnsureSuccessStatusCode();
        var tokens = await resp.Content.ReadFromJsonAsync<GoogleTokenResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Failed to refresh Gmail access token.");

        account.EncryptedAccessToken = _protector.Protect(tokens.AccessToken);
        account.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return tokens.AccessToken;
    }

    public async Task<EmailMessage> SendEmailAsync(
        Guid accountId, Guid requestingUserId, string to, string subject, string bodyText,
        Guid? contactId = null, string? threadId = null, CancellationToken ct = default)
    {
        var account = await _context.GmailAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == requestingUserId && a.IsActive, ct)
            ?? throw new KeyNotFoundException("Gmail account not found or not connected.");

        var accessToken = await GetValidAccessTokenAsync(account, ct);

        var mime = $"From: {account.EmailAddress}\r\n" +
                   $"To: {to}\r\n" +
                   $"Subject: {EncodeHeader(subject)}\r\n" +
                   "MIME-Version: 1.0\r\n" +
                   "Content-Type: text/plain; charset=\"UTF-8\"\r\n\r\n" +
                   bodyText;

        var raw = Base64UrlEncode(Encoding.UTF8.GetBytes(mime));

        var req = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var payload = threadId != null
            ? new { raw, threadId }
            : (object)new { raw };
        req.Content = JsonContent.Create(payload);

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var sent = await resp.Content.ReadFromJsonAsync<GmailSendResult>(JsonOpts, ct);

        var email = new EmailMessage
        {
            Id = Guid.NewGuid(),
            GmailAccountId = account.Id,
            ContactId = contactId,
            GmailMessageId = sent?.Id,
            GmailThreadId = sent?.ThreadId ?? threadId,
            Subject = subject,
            Snippet = bodyText.Length > 150 ? bodyText[..150] : bodyText,
            BodyText = bodyText,
            BodyHtml = string.Empty,
            FromAddress = account.EmailAddress,
            ToAddress = to,
            IsFromContact = false,
            IsRead = true,
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        _context.EmailMessages.Add(email);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Sent Gmail message from {From} to {To}", account.EmailAddress, to);
        return email;
    }

    public async Task<List<EmailMessage>> SyncInboxAsync(Guid accountId, Guid requestingUserId, int maxResults = 25, CancellationToken ct = default)
    {
        var account = await _context.GmailAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == requestingUserId && a.IsActive, ct)
            ?? throw new KeyNotFoundException("Gmail account not found or not connected.");

        var accessToken = await GetValidAccessTokenAsync(account, ct);

        var listReq = new HttpRequestMessage(HttpMethod.Get,
            $"https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults={maxResults}&labelIds=INBOX");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var listResp = await _http.SendAsync(listReq, ct);
        listResp.EnsureSuccessStatusCode();
        var list = await listResp.Content.ReadFromJsonAsync<GmailListResponse>(JsonOpts, ct);

        var result = new List<EmailMessage>();
        if (list?.Messages == null) return result;

        var contactsByEmail = await _context.Contacts.ToDictionaryAsync(c => c.Email.ToLowerInvariant(), c => c.Id, ct);

        foreach (var item in list.Messages)
        {
            var existing = await _context.EmailMessages.FirstOrDefaultAsync(m => m.GmailMessageId == item.Id, ct);
            if (existing != null) { result.Add(existing); continue; }

            var getReq = new HttpRequestMessage(HttpMethod.Get,
                $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{item.Id}?format=metadata&metadataHeaders=From&metadataHeaders=To&metadataHeaders=Subject&metadataHeaders=Date");
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var getResp = await _http.SendAsync(getReq, ct);
            if (!getResp.IsSuccessStatusCode) continue;
            var full = await getResp.Content.ReadFromJsonAsync<GmailMessageResponse>(JsonOpts, ct);
            if (full == null) continue;

            var headers = full.Payload?.Headers ?? new List<GmailHeader>();
            var from = headers.FirstOrDefault(h => h.Name.Equals("From", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
            var subject = headers.FirstOrDefault(h => h.Name.Equals("Subject", StringComparison.OrdinalIgnoreCase))?.Value ?? "(no subject)";
            var fromEmail = ExtractEmail(from);

            contactsByEmail.TryGetValue(fromEmail.ToLowerInvariant(), out var matchedContactId);

            var email = new EmailMessage
            {
                Id = Guid.NewGuid(),
                GmailAccountId = account.Id,
                ContactId = matchedContactId == Guid.Empty ? null : matchedContactId,
                GmailMessageId = full.Id,
                GmailThreadId = full.ThreadId,
                Subject = subject,
                Snippet = full.Snippet ?? "",
                BodyText = full.Snippet ?? "",
                BodyHtml = string.Empty,
                FromAddress = fromEmail,
                ToAddress = account.EmailAddress,
                IsFromContact = true,
                IsRead = !full.LabelIds.Contains("UNREAD"),
                ReceivedAt = DateTimeOffset.FromUnixTimeMilliseconds(full.InternalDate ?? 0).UtcDateTime,
                CreatedAt = DateTime.UtcNow,
            };
            _context.EmailMessages.Add(email);
            result.Add(email);
        }

        account.LastSyncedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return result.OrderByDescending(m => m.ReceivedAt).ToList();
    }

    public async Task<List<EmailMessage>> GetStoredMessagesAsync(Guid userId, Guid? contactId = null, int page = 1, int pageSize = 30, CancellationToken ct = default)
    {
        var accountIds = await _context.GmailAccounts.Where(a => a.UserId == userId).Select(a => a.Id).ToListAsync(ct);
        var query = _context.EmailMessages.Where(m => accountIds.Contains(m.GmailAccountId));
        if (contactId.HasValue) query = query.Where(m => m.ContactId == contactId.Value);

        return await query
            .OrderByDescending(m => m.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<EmailMessage?> GetMessageAsync(Guid emailMessageId, Guid userId, CancellationToken ct = default)
    {
        var accountIds = await _context.GmailAccounts.Where(a => a.UserId == userId).Select(a => a.Id).ToListAsync(ct);
        return await _context.EmailMessages.FirstOrDefaultAsync(m => m.Id == emailMessageId && accountIds.Contains(m.GmailAccountId), ct);
    }

    public async Task MarkAsReadAsync(Guid emailMessageId, Guid userId, CancellationToken ct = default)
    {
        var msg = await GetMessageAsync(emailMessageId, userId, ct);
        if (msg == null || msg.IsRead) return;
        msg.IsRead = true;
        await _context.SaveChangesAsync(ct);
    }

    private static string ExtractEmail(string headerValue)
    {
        var start = headerValue.IndexOf('<');
        var end = headerValue.IndexOf('>');
        if (start >= 0 && end > start) return headerValue[(start + 1)..end].Trim();
        return headerValue.Trim();
    }

    private static string EncodeHeader(string value) =>
        // RFC 2047 encoded-word for non-ASCII subjects; plain subjects pass through untouched.
        value.Any(c => c > 127) ? $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=" : value;

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private class GmailProfileResponse
    {
        [JsonPropertyName("emailAddress")] public string EmailAddress { get; set; } = "";
    }

    private class GmailSendResult
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("threadId")] public string? ThreadId { get; set; }
    }

    private class GmailListResponse
    {
        [JsonPropertyName("messages")] public List<GmailListItem>? Messages { get; set; }
    }

    private class GmailListItem
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
    }

    private class GmailMessageResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("threadId")] public string ThreadId { get; set; } = "";
        [JsonPropertyName("snippet")] public string? Snippet { get; set; }
        [JsonPropertyName("internalDate")] public long? InternalDate { get; set; }
        [JsonPropertyName("labelIds")] public List<string> LabelIds { get; set; } = new();
        [JsonPropertyName("payload")] public GmailPayload? Payload { get; set; }
    }

    private class GmailPayload
    {
        [JsonPropertyName("headers")] public List<GmailHeader>? Headers { get; set; }
    }

    private class GmailHeader
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("value")] public string Value { get; set; } = "";
    }
}
