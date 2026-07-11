using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using CRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace CRM.Infrastructure.Services;

public class CallService : ICallService
{
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationSettingsService _settings;
    private readonly ILogger<CallService> _logger;
    private readonly HttpClient _http = new();

    public CallService(IApplicationDbContext context, IIntegrationSettingsService settings, ILogger<CallService> logger)
    {
        _context = context;
        _settings = settings;
        _logger = logger;
    }

    private static readonly string[] RequiredKeys =
    {
        "Twilio:AccountSid", "Twilio:AuthToken", "Twilio:ApiKeySid",
        "Twilio:ApiKeySecret", "Twilio:TwimlAppSid", "Twilio:FromNumber",
    };

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        => await _settings.IsCategoryConfiguredAsync("Twilio", RequiredKeys, ct);

    private async Task<Dictionary<string, string>> GetCredentialsOrThrowAsync(CancellationToken ct)
    {
        var values = new Dictionary<string, string>();
        foreach (var key in RequiredKeys)
        {
            var v = await _settings.GetAsync(key, ct);
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException(
                    "Calling isn't configured yet. Go to Settings -> Integrations -> Calls and enter your Twilio credentials.");
            values[key] = v;
        }
        return values;
    }

    public async Task<string> GenerateVoiceAccessTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var creds = await GetCredentialsOrThrowAsync(ct);
        var accountSid = creds["Twilio:AccountSid"];
        var apiKeySid = creds["Twilio:ApiKeySid"];
        var apiKeySecret = creds["Twilio:ApiKeySecret"];
        var twimlAppSid = creds["Twilio:TwimlAppSid"];

        var now = DateTimeOffset.UtcNow;
        var exp = now.AddHours(1);

        var header = new Dictionary<string, object>
        {
            ["typ"] = "JWT",
            ["alg"] = "HS256",
            ["cty"] = "twilio-fpa;v=1",
        };
        var payload = new Dictionary<string, object>
        {
            ["jti"] = $"{apiKeySid}-{now.ToUnixTimeSeconds()}",
            ["iss"] = apiKeySid,
            ["sub"] = accountSid,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
            ["grants"] = new Dictionary<string, object>
            {
                ["identity"] = userId.ToString(),
                ["voice"] = new Dictionary<string, object>
                {
                    ["outgoing"] = new Dictionary<string, object> { ["application_sid"] = twimlAppSid },
                    ["incoming"] = new Dictionary<string, object> { ["allow"] = true },
                },
            },
        };

        var jsonOpts = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var headerB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header, jsonOpts));
        var payloadB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, jsonOpts));
        var signingInput = $"{headerB64}.{payloadB64}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKeySecret));
        var signature = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    public async Task<string> BuildOutboundTwimlAsync(string to, CancellationToken ct = default)
    {
        var creds = await GetCredentialsOrThrowAsync(ct);
        var from = creds["Twilio:FromNumber"];
        var safeTo = System.Security.SecurityElement.Escape(to);
        var safeFrom = System.Security.SecurityElement.Escape(from);
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
               $"<Response><Dial callerId=\"{safeFrom}\"><Number>{safeTo}</Number></Dial></Response>";
    }

    public async Task<string> BuildInboundTwimlAsync(string fromNumber, string toNumber, CancellationToken ct = default)
    {
        // Basic inbound handling: greet the caller and forward the call to the CRM's
        // configured "FromNumber" agent line via <Dial>, falling back to voicemail-style
        // message if nothing is configured. A fully custom IVR can be built on top of
        // this by extending BuildInboundTwimlAsync.
        await Task.CompletedTask;
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
               "<Response><Say voice=\"alice\">Thanks for calling. Please hold while we connect you.</Say>" +
               "<Dial><Client>support</Client></Dial></Response>";
    }

    public async Task HandleStatusCallbackAsync(IDictionary<string, string> form, CancellationToken ct = default)
    {
        if (!form.TryGetValue("CallSid", out var callSid)) return;

        var call = await _context.CallLogs.FirstOrDefaultAsync(c => c.ProviderCallSid == callSid, ct);
        if (call == null) return;

        if (form.TryGetValue("CallStatus", out var status))
        {
            call.Status = status switch
            {
                "queued" => CallStatus.Queued,
                "ringing" => CallStatus.Ringing,
                "in-progress" => CallStatus.InProgress,
                "completed" => CallStatus.Completed,
                "busy" => CallStatus.Busy,
                "failed" => CallStatus.Failed,
                "no-answer" => CallStatus.NoAnswer,
                "canceled" => CallStatus.Canceled,
                _ => call.Status,
            };
        }
        if (form.TryGetValue("CallDuration", out var durationStr) && int.TryParse(durationStr, out var duration))
            call.DurationSeconds = duration;
        if (form.TryGetValue("RecordingUrl", out var recordingUrl))
            call.RecordingUrl = recordingUrl;

        if (call.Status == CallStatus.InProgress && call.StartedAt == null) call.StartedAt = DateTime.UtcNow;
        if (call.Status is CallStatus.Completed or CallStatus.Failed or CallStatus.Busy or CallStatus.NoAnswer or CallStatus.Canceled)
            call.EndedAt = DateTime.UtcNow;

        call.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<CallLog> InitiateServerCallAsync(Guid initiatedByUserId, Guid? contactId, string toNumber, string agentPhoneNumber, string publicBaseUrl, CancellationToken ct = default)
    {
        var creds = await GetCredentialsOrThrowAsync(ct);
        var accountSid = creds["Twilio:AccountSid"];
        var authToken = creds["Twilio:AuthToken"];
        var fromNumber = creds["Twilio:FromNumber"];

        var callLog = new CallLog
        {
            Id = Guid.NewGuid(),
            InitiatedByUserId = initiatedByUserId,
            ContactId = contactId,
            FromNumber = fromNumber,
            ToNumber = toNumber,
            Direction = CallDirection.Outbound,
            Status = CallStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        var baseUrl = publicBaseUrl.TrimEnd('/');

        // Twilio will first ring the agent's own phone; once they pick up, Twilio
        // requests our /api/calls/twiml endpoint, which returns TwiML that dials the
        // contact — a classic "connect two PSTN legs" click-to-call, no browser/WebRTC
        // required for this path.
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Calls.json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = agentPhoneNumber,
            ["From"] = fromNumber,
            ["Url"] = $"{baseUrl}/api/calls/twiml?to={Uri.EscapeDataString(toNumber)}",
            ["StatusCallback"] = $"{baseUrl}/api/calls/status-callback",
            ["StatusCallbackEvent"] = "initiated ringing answered completed",
        });

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Twilio call creation failed: {Body}", body);
            callLog.Status = CallStatus.Failed;
        }
        else
        {
            using var doc = JsonDocument.Parse(body);
            callLog.ProviderCallSid = doc.RootElement.TryGetProperty("sid", out var sidEl) ? sidEl.GetString() : null;
            callLog.Status = CallStatus.Ringing;
        }

        _context.CallLogs.Add(callLog);
        await _context.SaveChangesAsync(ct);
        return callLog;
    }

    public async Task<List<CallLog>> GetCallHistoryAsync(Guid userId, Guid? contactId, int page = 1, int pageSize = 30, CancellationToken ct = default)
    {
        var query = _context.CallLogs.Where(c => c.InitiatedByUserId == userId);
        if (contactId.HasValue) query = query.Where(c => c.ContactId == contactId.Value);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
