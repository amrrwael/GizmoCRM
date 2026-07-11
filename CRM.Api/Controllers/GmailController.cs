using CRM.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/gmail")]
[Authorize]
public class GmailController : ControllerBase
{
    private readonly IGmailService _gmail;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GmailController> _logger;

    public GmailController(IGmailService gmail, ICurrentUserService currentUser, ILogger<GmailController> logger)
    {
        _gmail = gmail;
        _currentUser = currentUser;
        _logger = logger;
    }

    private string RedirectUri => $"{Request.Scheme}://{Request.Host}/api/gmail/callback";
    // The frontend origin to bounce the browser back to once OAuth finishes.
    private string FrontendOrigin => Request.Headers.TryGetValue("X-Frontend-Origin", out var o) && !string.IsNullOrWhiteSpace(o)
        ? o.ToString()
        : $"{Request.Scheme}://{Request.Host}";

    [HttpGet("connect-url")]
    public async Task<IActionResult> GetConnectUrl()
    {
        try
        {
            var url = await _gmail.GetAuthorizationUrlAsync(_currentUser.UserId, RedirectUri);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Anonymous: Google redirects the user's browser here directly (no auth header attached).
    // The `state` parameter carries the CRM user id that started the flow.
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error)
    {
        var frontendBase = FrontendOrigin;
        if (!string.IsNullOrEmpty(error))
            return Redirect($"{frontendBase}/integrations?gmail=error&reason={Uri.EscapeDataString(error)}");

        if (!Guid.TryParse(state, out var userId))
            return Redirect($"{frontendBase}/integrations?gmail=error&reason=invalid_state");

        try
        {
            await _gmail.HandleOAuthCallbackAsync(code, userId, RedirectUri);
            return Redirect($"{frontendBase}/integrations?gmail=connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail OAuth callback failed");
            return Redirect($"{frontendBase}/integrations?gmail=error&reason={Uri.EscapeDataString(ex.Message)}");
        }
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _gmail.GetAccountsAsync(_currentUser.UserId);
        return Ok(accounts.Select(a => new { a.Id, a.EmailAddress, a.IsActive, a.LastSyncedAt }));
    }

    [HttpDelete("accounts/{accountId}")]
    public async Task<IActionResult> Disconnect(Guid accountId)
    {
        await _gmail.DisconnectAsync(accountId, _currentUser.UserId);
        return Ok(new { success = true });
    }

    public record SendEmailRequest(Guid AccountId, string To, string Subject, string Body, Guid? ContactId, string? ThreadId);

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest request)
    {
        try
        {
            var email = await _gmail.SendEmailAsync(request.AccountId, _currentUser.UserId, request.To, request.Subject, request.Body, request.ContactId, request.ThreadId);
            return Ok(new { success = true, email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Gmail message");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("accounts/{accountId}/sync")]
    public async Task<IActionResult> Sync(Guid accountId, [FromQuery] int maxResults = 25)
    {
        try
        {
            var messages = await _gmail.SyncInboxAsync(accountId, _currentUser.UserId, maxResults);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Gmail inbox");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] Guid? contactId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var messages = await _gmail.GetStoredMessagesAsync(_currentUser.UserId, contactId, page, pageSize);
        return Ok(messages);
    }

    [HttpGet("messages/{id}")]
    public async Task<IActionResult> GetMessage(Guid id)
    {
        var message = await _gmail.GetMessageAsync(id, _currentUser.UserId);
        if (message == null) return NotFound();
        return Ok(message);
    }

    [HttpPost("messages/{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _gmail.MarkAsReadAsync(id, _currentUser.UserId);
        return Ok(new { success = true });
    }
}
