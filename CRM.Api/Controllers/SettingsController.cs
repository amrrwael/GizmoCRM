using CRM.Application.Common.Interfaces;
using CRM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

/// <summary>
/// Everything a non-technical admin needs to wire up Telegram, Gmail, and Calls
/// lives behind this controller — no appsettings.json editing, no environment
/// variables, no redeploys. Values are encrypted at rest (see IIntegrationSettingsService).
/// </summary>
[ApiController]
[Route("api/settings/integrations")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IIntegrationSettingsService _settings;
    private readonly ICurrentUserService _currentUser;

    public SettingsController(IIntegrationSettingsService settings, ICurrentUserService currentUser)
    {
        _settings = settings;
        _currentUser = currentUser;
    }

    private bool IsAdminOrManager => _currentUser.Role is UserRole.Admin or UserRole.Manager;

    /// <summary>Lightweight status check any authenticated user can call to know what's usable.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var telegram = !string.IsNullOrWhiteSpace(await _settings.GetAsync("Telegram:BotToken"));
        var gmail = await _settings.GetAsync("Gmail:ClientId") is { Length: > 0 } &&
                    await _settings.GetAsync("Gmail:ClientSecret") is { Length: > 0 };
        var twilioKeys = new[] { "Twilio:AccountSid", "Twilio:AuthToken", "Twilio:ApiKeySid", "Twilio:ApiKeySecret", "Twilio:TwimlAppSid", "Twilio:FromNumber" };
        var calls = await _settings.IsCategoryConfiguredAsync("Twilio", twilioKeys);

        return Ok(new { telegram, gmail, calls });
    }

    [HttpGet("{category}")]
    public async Task<IActionResult> GetCategory(string category)
    {
        if (!IsAdminOrManager) return Forbid();
        var values = await _settings.GetMaskedForCategoryAsync(category);
        return Ok(values);
    }

    public record SaveIntegrationRequest(string Category, Dictionary<string, string> Values);

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveIntegrationRequest request)
    {
        if (!IsAdminOrManager) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Category) || request.Values.Count == 0)
            return BadRequest(new { message = "Category and at least one value are required." });

        foreach (var (key, value) in request.Values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue; // skip blanks so masked values from the UI aren't overwritten
            await _settings.SetAsync($"{request.Category}:{key}", value, request.Category);
        }

        return Ok(new { success = true });
    }

    [HttpDelete("{category}/{key}")]
    public async Task<IActionResult> Delete(string category, string key)
    {
        if (!IsAdminOrManager) return Forbid();
        await _settings.DeleteAsync($"{category}:{key}");
        return Ok(new { success = true });
    }
}
