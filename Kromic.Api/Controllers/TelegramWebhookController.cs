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

                // Send latest gold rate to new users
                if (isNewUser)
                {
                    await SendLatestRateToUserAsync(chatId, cancellationToken);
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

                    // Send latest gold rate to new users
                    if (isNewUser)
                    {
                        await SendLatestRateToUserAsync(chatId, cancellationToken);
                    }
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

    private async Task SendLatestRateToUserAsync(string chatId, CancellationToken cancellationToken)
    {
        try
        {
            var currentRate = await goldRateService.GetCurrentAsync(cancellationToken);
            if (currentRate == null)
            {
                logger.LogInformation("No gold rate available to send to new user {ChatId}", chatId);
                return;
            }

            var istFetchedAt = TimeZoneInfo.ConvertTime(currentRate.FetchedAt, GetIndiaTimeZone());
            var message = $"<b>📈 Welcome! Here's the Latest Gold Rate</b>\n\n" +
                          $"<b>22K Gold Rate:</b> ₹{currentRate.R22KT:N2}\n" +
                          $"<i>Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>\n\n" +
                          $"<i>You'll receive daily updates at 11:00 AM IST</i>";

            await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
            logger.LogInformation("Sent welcome message with latest rate to new user {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending latest rate to new user {ChatId}", chatId);
            // Don't throw - we already added the user, just couldn't send initial rate
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
