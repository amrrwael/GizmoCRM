using CRM.Application.Common.Models;
using CRM.Domain.Entities;

namespace CRM.Application.Common.Interfaces;

public interface ITelegramService
{
    Task<TelegramMessage> SendMessageAsync(Guid contactId, string message);
    Task<List<TelegramChat>> GetChatsAsync(Guid? userId = null);
    Task<TelegramChat> GetChatByContactAsync(Guid contactId);
    Task<List<TelegramMessage>> GetMessagesAsync(Guid chatId, int page = 1, int pageSize = 50);
    Task MarkAsReadAsync(Guid messageId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<bool> SetWebhookAsync(string webhookUrl);
    Task<bool> IsContactConnectedAsync(Guid contactId);
    Task<bool> ConnectContactAsync(Guid contactId, string telegramUserId);
    Task HandleWebhookAsync(TelegramUpdateDto update);
}
