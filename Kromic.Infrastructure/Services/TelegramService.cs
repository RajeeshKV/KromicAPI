using System.Net.Http.Json;
using System.Text.Json;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class TelegramService(
    HttpClient httpClient,
    ITelegramUserService telegramUserService,
    IOptions<GoldRateOptions> options,
    ILogger<TelegramService> logger) : ITelegramService
{
    private readonly GoldRateOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> SendMessageAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            logger.LogWarning("Telegram bot token is not configured.");
            return false;
        }

        // Get chat IDs from database (users who have interacted with the bot)
        var chatIds = await telegramUserService.GetActiveChatIdsAsync(cancellationToken);
        
        // Also include configured chat IDs from environment variables
        var configuredChatIds = ResolveChatIds();
        
        // Combine and deduplicate
        var allChatIds = new HashSet<string>(chatIds);
        foreach (var id in configuredChatIds)
        {
            allChatIds.Add(id);
        }

        if (allChatIds.Count == 0)
        {
            logger.LogWarning("No Telegram chat IDs found in database or configuration.");
            return false;
        }

        var successCount = 0;
        var failureCount = 0;

        foreach (var chatId in allChatIds)
        {
            try
            {
                await SendMessageToTelegramAsync(chatId, message, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                failureCount++;
                logger.LogError(ex, "Exception occurred while sending Telegram message to chat ID: {ChatId}", chatId);
            }
        }

        logger.LogInformation(
            "Telegram message dispatch completed. Total: {Total}, Successful: {SuccessCount}, Failed: {FailureCount}",
            allChatIds.Count,
            successCount,
            failureCount);

        return successCount > 0;
    }

    public async Task<bool> SendMessageToChatIdAsync(
        string chatId,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            logger.LogWarning("Telegram bot token is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            logger.LogWarning("Chat ID is empty.");
            return false;
        }

        try
        {
            await SendMessageToTelegramAsync(chatId, message, cancellationToken);
            logger.LogInformation("Telegram message sent successfully to chat ID: {ChatId}", chatId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram message to chat ID: {ChatId}", chatId);
            return false;
        }
    }

    private async Task SendMessageToTelegramAsync(
        string chatId,
        string message,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            chat_id = chatId,
            text = message,
            parse_mode = "HTML"
        };

        var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/sendMessage";
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Telegram API returned {(int)response.StatusCode} {response.ReasonPhrase}. Error: {errorContent}");
        }
    }

    private List<string> ResolveChatIds()
    {
        var chatIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chatId in _options.TelegramChatIds)
        {
            AddChatId(chatId);
        }

        foreach (var chatId in _options.TelegramChatIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddChatId(chatId);
        }

        return chatIds.OrderBy(x => x).ToList();

        void AddChatId(string? id)
        {
            var trimmed = id?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                chatIds.Add(trimmed);
            }
        }
    }

    public async Task<bool> SendMessageWithMenuAsync(
        string chatId,
        string message,
        List<TelegramMenuRow> menu,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            logger.LogWarning("Telegram bot token is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            logger.LogWarning("Chat ID is empty.");
            return false;
        }

        try
        {
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML",
                reply_markup = new
                {
                    inline_keyboard = menu.Select(row => 
                        row.Buttons.Select(btn => 
                            new 
                            {
                                text = btn.Text,
                                callback_data = btn.CallbackData
                            }).ToArray()).ToArray()
                }
            };

            var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/sendMessage";
            using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Telegram API returned {(int)response.StatusCode} {response.ReasonPhrase}. Error: {errorContent}");
            }

            logger.LogInformation("Telegram message with menu sent successfully to chat ID: {ChatId}", chatId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram message with menu to chat ID: {ChatId}", chatId);
            return false;
        }
    }

    public async Task<bool> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        bool showAlert = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            logger.LogWarning("Telegram bot token is not configured.");
            return false;
        }

        try
        {
            var payload = new
            {
                callback_query_id = callbackQueryId,
                text = text,
                show_alert = showAlert
            };

            var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/answerCallbackQuery";
            using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Failed to answer callback query: {Error}", errorContent);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to answer callback query: {CallbackQueryId}", callbackQueryId);
            return false;
        }
    }
}
