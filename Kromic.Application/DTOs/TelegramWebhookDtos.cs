using System.Text.Json.Serialization;

namespace Kromic.Application.DTOs;

public class TelegramWebhookRequest
{
    [JsonPropertyName("update_id")]
    public int UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }

    [JsonPropertyName("my_chat_member")]
    public TelegramMyChatMember? MyChatMember { get; set; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; set; }
}

public class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("from")]
    public TelegramUserDTO? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; set; }

    [JsonPropertyName("date")]
    public int Date { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class TelegramMyChatMember
{
    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; set; }

    [JsonPropertyName("from")]
    public TelegramUserDTO? From { get; set; }

    [JsonPropertyName("date")]
    public int Date { get; set; }

    [JsonPropertyName("new_chat_member")]
    public TelegramChatMember? NewChatMember { get; set; }
}

public class TelegramUserDTO
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; set; }
}

public class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class TelegramChatMember
{
    [JsonPropertyName("user")]
    public TelegramUserDTO? User { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class TelegramCallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public TelegramUserDTO? From { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

public sealed class BroadcastRequest
{
    public string Message { get; set; } = string.Empty;
}

public sealed record TelegramFeedbackNotification(
    string ChatId,
    string? FirstName,
    string? LastName,
    string? Username,
    string Message,
    DateTimeOffset ReceivedAt);

public sealed record TelegramBotUserResponse(
    Guid Id,
    string ChatId,
    string? FirstName,
    string? LastName,
    string? Username,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastInteractedAt,
    string? Email,
    bool EmailAlertsEnabled,
    DateTimeOffset? EmailSubscribedAt,
    DateTimeOffset? EmailUnsubscribedAt);