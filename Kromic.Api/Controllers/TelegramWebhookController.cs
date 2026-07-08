using System.Net;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(
    ITelegramUserService telegramUserService,
    ITelegramService telegramService,
    ITransactionalEmailService emailService,
    IGoldRateService goldRateService,
    IGoldRateEmailSubscriptionService emailSubscriptionService,
    IMemoryCache memoryCache,
    IOptions<GoldRateOptions> goldRateOptions,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    private static readonly TimeSpan FeedbackCaptureWindow = TimeSpan.FromMinutes(10);
    private readonly GoldRateOptions _goldRateOptions = goldRateOptions.Value;

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook([FromBody] TelegramWebhookRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            logger.LogWarning("Received null webhook request");
            return Ok();
        }

        try
        {
            if (request.Message?.From?.Id > 0 && request.Message?.Chat?.Id > 0)
            {
                var chatId = request.Message.Chat.Id.ToString();
                var firstName = request.Message.From.FirstName;
                var lastName = request.Message.From.LastName;
                var username = request.Message.From.Username;
                var messageText = request.Message.Text ?? string.Empty;

                var isNewUser = (await telegramUserService.GetUserByChatIdAsync(chatId, cancellationToken)) == null;

                await telegramUserService.AddOrUpdateUserAsync(
                    chatId,
                    firstName,
                    lastName,
                    username,
                    cancellationToken);

                logger.LogInformation(
                    "Registered/updated Telegram user - ChatID: {ChatId}, Name: {Name}, Username: {Username}",
                    chatId,
                    $"{firstName} {lastName}".Trim(),
                    username);

                if (isNewUser)
                {
                    await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser);
                }

                if (messageText.StartsWith("/", StringComparison.Ordinal))
                {
                    await HandleCommandAsync(chatId, request.Message.From, messageText, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(messageText))
                {
                    if (!await TryCompleteFeedbackAsync(chatId, request.Message.From, messageText, cancellationToken))
                    {
                        await TryCompleteEmailAlertsSubscriptionAsync(chatId, messageText, cancellationToken);
                    }
                }
            }

            if (request.MyChatMember?.From?.Id > 0 && request.MyChatMember?.Chat?.Id > 0)
            {
                var chatId = request.MyChatMember.Chat.Id.ToString();
                var status = request.MyChatMember.NewChatMember?.Status ?? "unknown";
                var firstName = request.MyChatMember.From.FirstName;
                var lastName = request.MyChatMember.From.LastName;
                var username = request.MyChatMember.From.Username;

                if (status == "member")
                {
                    var isNewUser = (await telegramUserService.GetUserByChatIdAsync(chatId, cancellationToken)) == null;

                    await telegramUserService.AddOrUpdateUserAsync(
                        chatId,
                        firstName,
                        lastName,
                        username,
                        cancellationToken);

                    logger.LogInformation(
                        "User joined bot - ChatID: {ChatId}, Status: {Status}",
                        chatId,
                        status);

                    await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser);
                }
                else if (status == "left" || status == "kicked")
                {
                    logger.LogInformation(
                        "User left/kicked bot - ChatID: {ChatId}, Status: {Status}",
                        chatId,
                        status);
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Telegram webhook");
            return Ok();
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var count = await telegramUserService.GetActiveChatCountAsync(cancellationToken);
        return Ok(new { activeUsers = count });
    }

    private async Task HandleCommandAsync(
        string chatId,
        Kromic.Application.DTOs.TelegramUserDTO fromUser,
        string messageText,
        CancellationToken cancellationToken)
    {
        var trimmed = messageText.Trim();
        var commandToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var command = commandToken.Split('@')[0].ToLowerInvariant();
        var payload = trimmed.Length > commandToken.Length ? trimmed[commandToken.Length..].Trim() : string.Empty;

        if (command == "/currentrate")
        {
            await SendCurrentRateAsync(chatId, cancellationToken);
        }
        else if (command == "/lastonemonthrates")
        {
            await SendLastOneMonthRatesAsync(chatId, cancellationToken);
        }
        else if (command == "/emailalerts")
        {
            await StartEmailAlertsSubscriptionAsync(chatId, cancellationToken);
        }
        else if (command == "/feedback")
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                await StartFeedbackCaptureAsync(chatId, cancellationToken);
            }
            else
            {
                await SubmitFeedbackAsync(chatId, fromUser, payload, cancellationToken);
            }
        }
    }

    private async Task StartEmailAlertsSubscriptionAsync(string chatId, CancellationToken cancellationToken)
    {
        await emailSubscriptionService.StartEmailCaptureAsync(chatId, cancellationToken);

        const string message = "Please reply with your email address within 1 minute to receive gold-rate email alerts.";
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
        logger.LogInformation("Started email alert subscription capture for Telegram chat {ChatId}", chatId);
    }

    private async Task StartFeedbackCaptureAsync(string chatId, CancellationToken cancellationToken)
    {
        memoryCache.Set(GetFeedbackCacheKey(chatId), true, FeedbackCaptureWindow);
        await telegramService.SendMessageToChatIdAsync(
            chatId,
            "Please send your feedback in the next message. I will forward it to Admin.",
            cancellationToken);
        logger.LogInformation("Started feedback capture for Telegram chat {ChatId}", chatId);
    }

    private async Task<bool> TryCompleteFeedbackAsync(
        string chatId,
        Kromic.Application.DTOs.TelegramUserDTO fromUser,
        string messageText,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetFeedbackCacheKey(chatId);
        if (!memoryCache.TryGetValue<bool>(cacheKey, out _))
        {
            return false;
        }

        memoryCache.Remove(cacheKey);
        await SubmitFeedbackAsync(chatId, fromUser, messageText, cancellationToken);
        return true;
    }

    private async Task SubmitFeedbackAsync(
        string chatId,
        Kromic.Application.DTOs.TelegramUserDTO fromUser,
        string messageText,
        CancellationToken cancellationToken)
    {
        var feedbackText = messageText.Trim();
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Feedback cannot be empty. Send /feedback to try again.", cancellationToken);
            return;
        }

        var feedback = new TelegramFeedbackNotification(
            chatId,
            fromUser.FirstName,
            fromUser.LastName,
            fromUser.Username,
            feedbackText,
            DateTimeOffset.UtcNow);

        var mailSent = false;
        var telegramSent = false;

        try
        {
            await emailService.SendTelegramFeedbackAsync(feedback, cancellationToken);
            mailSent = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram feedback email for chat {ChatId}", chatId);
        }

        try
        {
            telegramSent = await telegramService.SendMessageToChatIdAsync(
                _goldRateOptions.FeedbackTelegramChatId,
                BuildFeedbackTelegramMessage(feedback),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward Telegram feedback to owner chat for user chat {ChatId}", chatId);
        }

        if (mailSent || telegramSent)
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Thanks, I received your feedback and forwarded it.", cancellationToken);
        }
        else
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Sorry, I could not forward the feedback right now. Please try again later.", cancellationToken);
        }
    }

    private async Task TryCompleteEmailAlertsSubscriptionAsync(string chatId, string messageText, CancellationToken cancellationToken)
    {
        var result = await emailSubscriptionService.TryCompleteEmailCaptureAsync(chatId, messageText, cancellationToken);
        var response = result.Status switch
        {
            EmailAlertSubscriptionStatus.Subscribed => $"Email alerts enabled for {result.Email}. You can unsubscribe from any future email.",
            EmailAlertSubscriptionStatus.InvalidEmail => "That does not look like a valid email address. Please send a valid email within 1 minute.",
            EmailAlertSubscriptionStatus.Expired => "The email alert request expired. Send /emailalerts again to subscribe.",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(response))
        {
            await telegramService.SendMessageToChatIdAsync(chatId, response, cancellationToken);
        }
    }

    private async Task SendLatestRateToUserAsync(string chatId, CancellationToken cancellationToken, bool isNewUser = false)
    {
        try
        {
            var currentRate = await goldRateService.GetCurrentAsync(cancellationToken);
            if (currentRate == null)
            {
                logger.LogInformation("No gold rate available to send to user {ChatId}", chatId);
                return;
            }

            var istFetchedAt = TimeZoneInfo.ConvertTime(currentRate.FetchedAt, GetIndiaTimeZone());
            var eightGramRate = currentRate.R22KT * 8;

            var title = isNewUser ? "Welcome! Here's the Latest Gold Rate" : "Current Gold Rate";
            var message = $"<b>{title}</b>\n\n" +
                          "<b>22K Gold Rate</b>\n" +
                          $"1g: Rs. {currentRate.R22KT:N2}\n" +
                          $"8g: Rs. {eightGramRate:N2}\n" +
                          $"<i>Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>";

            if (isNewUser)
            {
                message += "\n\n<i>You'll receive daily updates when the rate changes.</i>";
            }

            await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
            logger.LogInformation("Sent gold rate message to user {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending rate to user {ChatId}", chatId);
        }
    }

    private async Task SendCurrentRateAsync(string chatId, CancellationToken cancellationToken)
    {
        await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser: false);
    }

    private async Task SendLastOneMonthRatesAsync(string chatId, CancellationToken cancellationToken)
    {
        try
        {
            var indiaTimeZone = GetIndiaTimeZone();
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);
            var thirtyDaysAgo = now.AddDays(-30);

            var history = await goldRateService.GetHistoryAsync(
                range: null,
                from: thirtyDaysAgo,
                to: now,
                cancellationToken);

            if (history.Items == null || history.Items.Count == 0)
            {
                var noDataMessage = "<b>Last 30 Days Gold Rate Analysis</b>\n\n" +
                                    "<i>No data available for the past 30 days.</i>";
                await telegramService.SendMessageToChatIdAsync(chatId, noDataMessage, cancellationToken);
                logger.LogInformation("Sent no-data report to user {ChatId}", chatId);
                return;
            }

            var dateGrouped = new Dictionary<string, decimal>();
            foreach (var rate in history.Items)
            {
                var istDate = TimeZoneInfo.ConvertTime(rate.FetchedAt, indiaTimeZone);
                var dateKey = istDate.ToString("dd MMM yyyy");
                if (!dateGrouped.ContainsKey(dateKey))
                {
                    dateGrouped[dateKey] = rate.R22KT;
                }
            }

            var sortedDates = dateGrouped.Keys.ToList();
            sortedDates.Sort();

            var tableHeader = "<b>Last 30 Days Gold Rate Analysis</b>\n\n";
            tableHeader += "<code>Date          | 1g 22K | 8g 22K\n";
            tableHeader += "--------------------------------------\n";

            var tableLines = new List<string> { tableHeader };
            var currentTableMessage = tableHeader;

            foreach (var dateKey in sortedDates)
            {
                var oneGramRate = dateGrouped[dateKey];
                var eightGramRate = oneGramRate * 8;
                var line = $"{dateKey,-13} | {oneGramRate,7:N0} | {eightGramRate,7:N0}\n";

                if ((currentTableMessage + line + "</code>").Length > 4000)
                {
                    currentTableMessage += "</code>";
                    tableLines.Add(currentTableMessage);
                    currentTableMessage = "<code>" + line;
                }
                else
                {
                    currentTableMessage += line;
                }
            }

            if (!currentTableMessage.EndsWith("</code>"))
            {
                currentTableMessage += "</code>";
            }
            tableLines.Add(currentTableMessage);

            tableLines = tableLines.Where(m => !string.IsNullOrWhiteSpace(m) && m != "<code></code>").ToList();

            foreach (var msg in tableLines)
            {
                await telegramService.SendMessageToChatIdAsync(chatId, msg, cancellationToken);
            }

            logger.LogInformation("Sent last-month-rates report with {Count} unique dates to user {ChatId}", dateGrouped.Count, chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending last-month-rates to user {ChatId}", chatId);
            var errorMessage = "<b>Error</b>\n\nFailed to generate report. Please try again later.";
            await telegramService.SendMessageToChatIdAsync(chatId, errorMessage, cancellationToken);
        }
    }

    private static string BuildFeedbackTelegramMessage(TelegramFeedbackNotification feedback)
    {
        var displayName = string.Join(" ", new[] { feedback.FirstName, feedback.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)))
            .Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.IsNullOrWhiteSpace(feedback.Username) ? feedback.ChatId : feedback.Username;
        }

        var username = string.IsNullOrWhiteSpace(feedback.Username) ? "-" : $"@{feedback.Username}";
        return "<b>Telegram feedback received</b>\n\n" +
               $"<b>From:</b> {Html(displayName)} ({Html(username)})\n" +
               $"<b>Chat ID:</b> {Html(feedback.ChatId)}\n" +
               $"<b>Received:</b> {feedback.ReceivedAt:dd MMM yyyy, hh:mm tt} UTC\n\n" +
               "<b>Message:</b>\n" +
               Html(Truncate(feedback.Message, 3200));
    }

    private static string GetFeedbackCacheKey(string chatId) => $"telegram-feedback:{chatId}";

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static TimeZoneInfo GetIndiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
    }
}