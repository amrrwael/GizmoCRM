using CRM.Domain.Entities;

namespace CRM.Application.Common.Interfaces;

/// <summary>
/// Voice calling backed by Twilio's REST API, called directly over HttpClient
/// (no Twilio SDK dependency). Supports two flows:
///  1) In-browser calling: the frontend loads Twilio's Voice JS SDK and uses a
///     short-lived access token from GenerateVoiceAccessTokenAsync to place/receive
///     calls straight from the browser tab (no phone needed).
///  2) Click-to-call: InitiateServerCallAsync rings the agent's own phone first,
///     then bridges it to the contact once answered — useful if someone would
///     rather use their desk phone or mobile than the browser.
/// All Twilio credentials come from Settings -> Integrations -> Calls (Twilio).
/// </summary>
public interface ICallService
{
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);

    Task<string> GenerateVoiceAccessTokenAsync(Guid userId, CancellationToken ct = default);

    Task<string> BuildOutboundTwimlAsync(string to, CancellationToken ct = default);

    Task<string> BuildInboundTwimlAsync(string fromNumber, string toNumber, CancellationToken ct = default);

    Task HandleStatusCallbackAsync(IDictionary<string, string> form, CancellationToken ct = default);

    Task<CallLog> InitiateServerCallAsync(Guid initiatedByUserId, Guid? contactId, string toNumber, string agentPhoneNumber, string publicBaseUrl, CancellationToken ct = default);

    Task<List<CallLog>> GetCallHistoryAsync(Guid userId, Guid? contactId, int page = 1, int pageSize = 30, CancellationToken ct = default);
}
