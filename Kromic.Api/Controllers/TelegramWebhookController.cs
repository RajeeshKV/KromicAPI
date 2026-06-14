using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(
    ITelegramUserService telegramUserService,
    ITelegramService telegramService,
    IGoldRateService goldRateService,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
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
            // Handle /start command or user interaction
            if (request.Message?.From?.Id > 0 && request.Message?.Chat?.Id > 0)
            {
                var chatId = request.Message.Chat.Id.ToString();
                var firstName = request.Message.From.FirstName;
                var lastName = request.Message.From.LastName;
                var username = request.Message.From.Username;
                var messageText = request.Message.Text ?? "";

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

                // Send welcome message to new users
                if (isNewUser)
                {
                    await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser);
                }

                // Handle commands (messages starting with /)
                if (messageText.StartsWith("/"))
                {
                    var command = messageText.Split(' ', '@')[0].ToLowerInvariant();

                    if (command == "/currentrate")
                    {
                        await SendCurrentRateAsync(chatId, cancellationToken);
                    }
                    else if (command == "/lastonemonthrates")
                    {
                        await SendLastOneMonthRatesAsync(chatId, cancellationToken);
                    }
                }
            }

            // Handle user joining/leaving via my_chat_member
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

                    // Send current gold rate to users who join
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
            return Ok(); // Always return OK to prevent Telegram from retrying
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var count = await telegramUserService.GetActiveChatCountAsync(cancellationToken);
        return Ok(new { activeUsers = count });
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
            
            string message;
            if (isNewUser)
            {
                message = $"<b>📈 Welcome! Here's the Latest Gold Rate</b>\n\n" +
                          $"<b>22K Gold Rate:</b> ₹{currentRate.R22KT:N2}\n" +
                          $"<i>Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>\n\n" +
                          $"<i>You'll receive daily updates at 11:00 AM IST</i>";
            }
            else
            {
                message = $"<b>📈 Current Gold Rate</b>\n\n" +
                          $"<b>22K Gold Rate:</b> ₹{currentRate.R22KT:N2}\n" +
                          $"<i>Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>";
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

            // Fetch 30 days of gold rate history
            var history = await goldRateService.GetHistoryAsync(
                range: null,
                from: thirtyDaysAgo,
                to: now,
                cancellationToken);

            if (history.Items == null || history.Items.Count == 0)
            {
                var noDataMessage = "<b>📊 Last 30 Days Gold Rate Analysis</b>\n\n" +
                                    "<i>No data available for the past 30 days.</i>";
                await telegramService.SendMessageToChatIdAsync(chatId, noDataMessage, cancellationToken);
                logger.LogInformation("Sent no-data report to user {ChatId}", chatId);
                return;
            }

            // Group by date and pick the last rate of each day
            var dateGrouped = new Dictionary<string, decimal>();
            foreach (var rate in history.Items)
            {
                var istDate = TimeZoneInfo.ConvertTime(rate.FetchedAt, indiaTimeZone);
                var dateKey = istDate.ToString("dd MMM yyyy");
                // Always take the last one (list is sorted by FetchedAt descending, so first occurrence is the latest)
                if (!dateGrouped.ContainsKey(dateKey))
                {
                    dateGrouped[dateKey] = rate.R22KT;
                }
            }

            // Sort by date ascending for display
            var sortedDates = dateGrouped.Keys.ToList();
            sortedDates.Sort();

            // Build table format
            var tableHeader = "<b>📊 Last 30 Days Gold Rate Analysis</b>\n\n";
            tableHeader += "<code>Date          | 22K Rate\n";
            tableHeader += "─────────────────────────────\n";

            var tableLines = new List<string> { tableHeader };
            var currentTableMessage = tableHeader;

            foreach (var dateKey in sortedDates)
            {
                var rate = dateGrouped[dateKey];
                var line = $"{dateKey,-13} │ ₹{rate:N2}\n";

                // Check if adding this line would exceed message limit
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

            // Close the last message
            if (!currentTableMessage.EndsWith("</code>"))
            {
                currentTableMessage += "</code>";
            }
            tableLines.Add(currentTableMessage);

            // Remove empty messages
            tableLines = tableLines.Where(m => !string.IsNullOrWhiteSpace(m) && m != "<code></code>").ToList();

            // Send all messages
            foreach (var msg in tableLines)
            {
                await telegramService.SendMessageToChatIdAsync(chatId, msg, cancellationToken);
            }

            logger.LogInformation("Sent last-month-rates report with {Count} unique dates to user {ChatId}", dateGrouped.Count, chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending last-month-rates to user {ChatId}", chatId);
            var errorMessage = "<b>❌ Error</b>\n\nFailed to generate report. Please try again later.";
            await telegramService.SendMessageToChatIdAsync(chatId, errorMessage, cancellationToken);
        }
    }

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
