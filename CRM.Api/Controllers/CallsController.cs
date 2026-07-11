using CRM.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/calls")]
public class CallsController : ControllerBase
{
    private readonly ICallService _calls;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CallsController> _logger;

    public CallsController(ICallService calls, ICurrentUserService currentUser, ILogger<CallsController> logger)
    {
        _calls = calls;
        _currentUser = currentUser;
        _logger = logger;
    }

    private string PublicBaseUrl => $"{Request.Scheme}://{Request.Host}";

    [Authorize]
    [HttpGet("configured")]
    public async Task<IActionResult> IsConfigured() => Ok(new { configured = await _calls.IsConfiguredAsync() });

    /// <summary>Short-lived token the browser's Twilio Voice SDK uses to place/receive calls.</summary>
    [Authorize]
    [HttpGet("voice-token")]
    public async Task<IActionResult> GetVoiceToken()
    {
        try
        {
            var token = await _calls.GenerateVoiceAccessTokenAsync(_currentUser.UserId);
            return Ok(new { token, identity = _currentUser.UserId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public record ClickToCallRequest(Guid? ContactId, string ToNumber, string AgentPhoneNumber);

    [Authorize]
    [HttpPost("click-to-call")]
    public async Task<IActionResult> ClickToCall([FromBody] ClickToCallRequest request)
    {
        try
        {
            var call = await _calls.InitiateServerCallAsync(_currentUser.UserId, request.ContactId, request.ToNumber, request.AgentPhoneNumber, PublicBaseUrl);
            return Ok(call);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Click-to-call failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] Guid? contactId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var history = await _calls.GetCallHistoryAsync(_currentUser.UserId, contactId, page, pageSize);
        return Ok(history);
    }

    // ── Twilio webhooks (called by Twilio's servers, not the frontend — no JWT) ──

    [AllowAnonymous]
    [HttpPost("twiml")]
    [HttpGet("twiml")]
    public async Task<IActionResult> Twiml([FromQuery] string to)
    {
        var twiml = await _calls.BuildOutboundTwimlAsync(to);
        return Content(twiml, "application/xml");
    }

    [AllowAnonymous]
    [HttpPost("twiml/inbound")]
    public async Task<IActionResult> TwimlInbound([FromForm] string? From, [FromForm] string? To)
    {
        var twiml = await _calls.BuildInboundTwimlAsync(From ?? "", To ?? "");
        return Content(twiml, "application/xml");
    }

    [AllowAnonymous]
    [HttpPost("status-callback")]
    public async Task<IActionResult> StatusCallback()
    {
        var form = Request.HasFormContentType
            ? Request.Form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString())
            : new Dictionary<string, string>();
        await _calls.HandleStatusCallbackAsync(form);
        return Ok();
    }
}
