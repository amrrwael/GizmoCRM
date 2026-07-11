using CRM.Application.Common.Interfaces;
using CRM.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/telegram-webhook")]
[AllowAnonymous]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramService _telegramService;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(ITelegramService telegramService, ILogger<TelegramWebhookController> logger)
    {
        _telegramService = telegramService;
        _logger = logger;
    }

    // Bound to a strongly-typed DTO instead of `dynamic`/JsonElement — see
    // TelegramService.HandleWebhookAsync for why the old approach silently
    // dropped every incoming message.
    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromBody] TelegramUpdateDto update)
    {
        try
        {
            _logger.LogInformation("Received Telegram webhook, update_id={UpdateId}", update.UpdateId);
            await _telegramService.HandleWebhookAsync(update);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram webhook");
            // Telegram retries aggressively on non-2xx; return 200 anyway once logged
            // so a single bad update doesn't get retried forever.
            return Ok();
        }
    }
}
