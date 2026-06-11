using System.Net.Http.Json;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class TelegramService(
    HttpClient httpClient,
    IOptions<GoldRateOptions> options,
    ILogger<TelegramService> logger) : ITelegramService
{
    private readonly GoldRateOptions _options = options.Value;

    public async Task<bool> SendMessageAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            logger.LogWarning("Telegram bot token is not configured.");
            return false;
        }

        var chatIds = ResolveChatIds();
        if (chatIds.Count == 0)
        {
            logger.LogWarning("No Telegram chat IDs configured.");
            return false;
        }

        var successCount = 0;
        var failureCount = 0;

        foreach (var chatId in chatIds)
        {
            try
            {
                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML"
                };

                var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/sendMessage";
                using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    successCount++;
                    logger.LogInformation("Telegram message sent successfully to chat ID: {ChatId}", chatId);
                }
                else
                {
                    failureCount++;
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogWarning(
                        "Failed to send Telegram message to chat ID {ChatId}. Status: {StatusCode}. Error: {Error}",
                        chatId,
                        response.StatusCode,
                        errorContent);
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                logger.LogError(ex, "Exception occurred while sending Telegram message to chat ID: {ChatId}", chatId);
            }
        }

        logger.LogInformation(
            "Telegram message dispatch completed. Successful: {SuccessCount}, Failed: {FailureCount}",
            successCount,
            failureCount);

        return successCount > 0;
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
}
