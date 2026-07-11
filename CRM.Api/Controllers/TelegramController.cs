using CRM.Application.Common.Interfaces;
using CRM.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelegramController : ControllerBase
{
    private readonly ITelegramService _telegramService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        ITelegramService telegramService,
        ICurrentUserService currentUser,
        ILogger<TelegramController> logger)
    {
        _telegramService = telegramService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("chats")]
    public async Task<IActionResult> GetChats()
    {
        var chats = await _telegramService.GetChatsAsync(_currentUser.UserId);
        return Ok(chats);
    }

    [HttpGet("chats/{contactId}")]
    public async Task<IActionResult> GetChatByContact(Guid contactId)
    {
        try
        {
            var chat = await _telegramService.GetChatByContactAsync(contactId);
            return Ok(chat);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("messages/{contactId}")]
    public async Task<IActionResult> GetMessages(Guid contactId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var messages = await _telegramService.GetMessagesAsync(contactId, page, pageSize);
        return Ok(messages);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var message = await _telegramService.SendMessageAsync(request.ContactId, request.Message);
            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram message");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("mark-read/{messageId}")]
    public async Task<IActionResult> MarkAsRead(Guid messageId)
    {
        await _telegramService.MarkAsReadAsync(messageId);
        return Ok(new { success = true });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _telegramService.GetUnreadCountAsync(_currentUser.UserId);
        return Ok(new { count });
    }

    [HttpPost("connect")]
    public async Task<IActionResult> ConnectContact([FromBody] ConnectContactRequest request)
    {
        try
        {
            var result = await _telegramService.ConnectContactAsync(request.ContactId, request.TelegramUserId);
            return Ok(new { success = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("is-connected/{contactId}")]
    public async Task<IActionResult> IsContactConnected(Guid contactId)
    {
        var isConnected = await _telegramService.IsContactConnectedAsync(contactId);
        return Ok(new { isConnected });
    }

    public record SetWebhookRequest(string WebhookUrl);

    /// <summary>
    /// Called once from the Integrations page after the bot token is saved. Tells
    /// Telegram where to POST incoming messages — the app's own /api/telegram-webhook
    /// endpoint — so no manual webhook setup via curl/BotFather is ever required.
    /// </summary>
    [HttpPost("set-webhook")]
    public async Task<IActionResult> SetWebhook([FromBody] SetWebhookRequest request)
    {
        var ok = await _telegramService.SetWebhookAsync(request.WebhookUrl);
        return ok ? Ok(new { success = true }) : StatusCode(500, new { success = false });
    }
}

public record SendMessageRequest(Guid ContactId, string Message);
public record ConnectContactRequest(Guid ContactId, string TelegramUserId);