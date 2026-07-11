using System.Text.Json.Serialization;

namespace CRM.Application.Common.Models;

// Strongly-typed mirror of the subset of the Telegram Bot API "Update" object
// that the CRM cares about. Binding the webhook body to this type (instead of
// `dynamic`) is what actually lets ASP.NET Core deserialize incoming updates.
public class TelegramUpdateDto
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }

    [JsonPropertyName("edited_message")]
    public TelegramMessageDto? EditedMessage { get; set; }
}

public class TelegramMessageDto
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("from")]
    public TelegramUserDto? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatDto? Chat { get; set; }

    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class TelegramUserDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class TelegramChatDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
