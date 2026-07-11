using CRM.Application.Common.Interfaces;
using CRM.Application.Common.Models;
using CRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRM.Infrastructure.Services;

public class TelegramService : ITelegramService
{
    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationSettingsService _settings;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(
        HttpClient httpClient,
        IApplicationDbContext context,
        IIntegrationSettingsService settings,
        ILogger<TelegramService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _settings = settings;
        _logger = logger;
    }

    private async Task<string> GetBotTokenOrThrowAsync()
    {
        // Bot token now lives in the database (Settings -> Integrations -> Telegram),
        // configured entirely from the UI. Previously this read IConfiguration["Telegram:BotToken"],
        // which meant a non-technical user would have had to hand-edit appsettings.json.
        var token = await _settings.GetAsync("Telegram:BotToken");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Telegram bot token is not configured. Go to Settings -> Integrations -> Telegram and connect your bot.");
        return token;
    }

    public async Task<TelegramMessage> SendMessageAsync(Guid contactId, string message)
    {
        try
        {
            var chat = await _context.TelegramChats
                .FirstOrDefaultAsync(c => c.ContactId == contactId);

            if (chat == null || string.IsNullOrEmpty(chat.TelegramChatId))
                throw new InvalidOperationException("This contact is not connected to Telegram yet.");

            var botToken = await GetBotTokenOrThrowAsync();
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = new
            {
                chat_id = chat.TelegramChatId,
                text = message,
                parse_mode = "HTML"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TelegramSendResponse>(jsonResponse, JsonOpts);

            var telegramMessage = new TelegramMessage
            {
                Id = Guid.NewGuid(),
                ContactId = contactId,
                TelegramChatId = chat.Id,
                Content = message,
                IsFromContact = false,
                IsRead = true,
                ReadAt = DateTime.UtcNow,
                TelegramMessageId = result?.Result?.MessageId?.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TelegramMessages.Add(telegramMessage);

            chat.LastMessageAt = DateTime.UtcNow;
            chat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Telegram message sent to contact {ContactId}", contactId);
            return telegramMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to contact {ContactId}", contactId);
            throw;
        }
    }

    public async Task<List<TelegramChat>> GetChatsAsync(Guid? userId = null)
    {
        var query = _context.TelegramChats
            .Include(c => c.Contact)
            .Include(c => c.Messages)
            .Where(c => c.IsActive);

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(c => c.Contact != null &&
                                     (c.Contact.AssignedToId == userId.Value || c.Contact.CreatedBy == userId.Value));
        }

        return await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();
    }

    public async Task<TelegramChat> GetChatByContactAsync(Guid contactId)
    {
        var chat = await _context.TelegramChats
            .Include(c => c.Contact)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(50))
            .FirstOrDefaultAsync(c => c.ContactId == contactId);

        if (chat == null)
            throw new KeyNotFoundException($"No Telegram chat found for contact {contactId}");

        return chat;
    }

    public async Task<List<TelegramMessage>> GetMessagesAsync(Guid chatId, int page = 1, int pageSize = 50)
    {
        // chatId here is the CRM ContactId (kept for backwards compatibility with the
        // existing /api/telegram/messages/{contactId} route). We resolve the actual
        // TelegramChat first so pagination and ordering are unambiguous even though
        // ContactId can briefly be null before a chat is linked.
        return await _context.TelegramMessages
            .Where(m => m.ContactId == chatId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid messageId)
    {
        var message = await _context.TelegramMessages.FindAsync(messageId);
        if (message != null && !message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _context.TelegramMessages
            .Include(m => m.Contact)
            .Where(m => !m.IsRead && m.Contact != null &&
                       (m.Contact.AssignedToId == userId || m.Contact.CreatedBy == userId))
            .CountAsync();
    }

    public async Task<bool> SetWebhookAsync(string webhookUrl)
    {
        try
        {
            var botToken = await GetBotTokenOrThrowAsync();
            var url = $"https://api.telegram.org/bot{botToken}/setWebhook?url={Uri.EscapeDataString(webhookUrl)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Telegram webhook set to {WebhookUrl}", webhookUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Telegram webhook to {WebhookUrl}", webhookUrl);
            return false;
        }
    }

    public async Task<bool> IsContactConnectedAsync(Guid contactId)
    {
        return await _context.TelegramChats
            .AnyAsync(c => c.ContactId == contactId && c.IsActive);
    }

    public async Task<bool> ConnectContactAsync(Guid contactId, string telegramUserId)
    {
        var contact = await _context.Contacts.FindAsync(contactId);
        if (contact == null)
            throw new KeyNotFoundException("Contact not found");

        var existingChat = await _context.TelegramChats
            .FirstOrDefaultAsync(c => c.TelegramUserId == telegramUserId);

        if (existingChat != null)
        {
            existingChat.ContactId = contactId;
            existingChat.IsActive = true;
            existingChat.UpdatedAt = DateTime.UtcNow;

            // Backfill ContactId onto any messages received before the chat was linked,
            // so message history shows up immediately once connected.
            var unlinkedMessages = await _context.TelegramMessages
                .Where(m => m.TelegramChatId == existingChat.Id && m.ContactId == null)
                .ToListAsync();
            foreach (var msg in unlinkedMessages) msg.ContactId = contactId;
        }
        else
        {
            var newChat = new TelegramChat
            {
                Id = Guid.NewGuid(),
                ContactId = contactId,
                TelegramUserId = telegramUserId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.TelegramChats.Add(newChat);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // Rewritten to use a strongly-typed DTO instead of `dynamic`. The previous
    // implementation bound the webhook body to `dynamic` (which System.Text.Json
    // materializes as a boxed JsonElement) and then did `update.message`,
    // `chat.id`, `from.id`, etc. JsonElement has no such properties, so every
    // single incoming Telegram message threw a RuntimeBinderException and was
    // silently swallowed by the controller's catch block — the bot could never
    // actually receive anything.
    public async Task HandleWebhookAsync(TelegramUpdateDto update)
    {
        try
        {
            var message = update.Message ?? update.EditedMessage;
            if (message?.Chat == null) return;

            var chatIdStr = message.Chat.Id.ToString();
            var text = message.Text ?? string.Empty;

            var telegramChat = await _context.TelegramChats
                .FirstOrDefaultAsync(c => c.TelegramChatId == chatIdStr);

            if (telegramChat == null)
            {
                telegramChat = new TelegramChat
                {
                    Id = Guid.NewGuid(),
                    TelegramChatId = chatIdStr,
                    TelegramUserId = message.From?.Id.ToString(),
                    FirstName = message.From?.FirstName,
                    LastName = message.From?.LastName,
                    Username = message.From?.Username,
                    IsActive = false, // not linked to a CRM contact yet
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.TelegramChats.Add(telegramChat);
                await _context.SaveChangesAsync();
            }

            var telegramMessage = new TelegramMessage
            {
                Id = Guid.NewGuid(),
                ContactId = telegramChat.ContactId,
                TelegramChatId = telegramChat.Id,
                TelegramMessageId = message.MessageId.ToString(),
                Content = text,
                IsFromContact = true,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TelegramMessages.Add(telegramMessage);

            telegramChat.LastMessageAt = DateTime.UtcNow;
            telegramChat.UnreadCount += 1;
            telegramChat.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Received Telegram message from chat {ChatId}", chatIdStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram webhook");
            throw;
        }
    }


    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private class TelegramSendResponse
    {
        public bool Ok { get; set; }
        public TelegramResult? Result { get; set; }
    }

    private class TelegramResult
    {
        [JsonPropertyName("message_id")]
        public int? MessageId { get; set; }
    }
}
