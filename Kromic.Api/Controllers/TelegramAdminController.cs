using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kromic.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/telegram")]
public sealed class TelegramAdminController(
    ITelegramUserService telegramUserService,
    ITelegramService telegramService,
    IUserSettingsService userSettingsService,
    ILogger<TelegramAdminController> logger) : ControllerBase
{
    [HttpGet("users")]
    public Task<IReadOnlyList<TelegramBotUserResponse>> Users(CancellationToken cancellationToken) =>
        telegramUserService.GetUsersWithEmailSubscriptionsAsync(cancellationToken);

    [HttpPost("broadcast")]
    public async Task<IActionResult> LocalizedBroadcast([FromBody] LocalizedBroadcastRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnglishMessage) && string.IsNullOrWhiteSpace(request.MalayalamMessage))
        {
            return BadRequest(new { error = "At least one language message is required" });
        }

        try
        {
            var chatIds = await telegramUserService.GetActiveChatIdsAsync(cancellationToken);
            var successCount = 0;
            var failureCount = 0;
            var englishCount = 0;
            var malayalamCount = 0;

            foreach (var chatId in chatIds)
            {
                try
                {
                    var userSettings = await userSettingsService.GetByChatIdAsync(chatId, cancellationToken);
                    var language = userSettings?.Language ?? "en";
                    
                    var message = language == "ml" 
                        ? request.MalayalamMessage 
                        : request.EnglishMessage;

                    // Fallback to English if the requested language message is empty
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = language == "ml" ? request.EnglishMessage : request.MalayalamMessage;
                    }

                    // Final fallback to English if both are empty for this user
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = request.EnglishMessage;
                    }

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        logger.LogWarning("No message available for chat ID: {ChatId} with language: {Language}", chatId, language);
                        failureCount++;
                        continue;
                    }

                    var sent = await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
                    if (sent)
                    {
                        successCount++;
                        if (language == "ml")
                        {
                            malayalamCount++;
                        }
                        else
                        {
                            englishCount++;
                        }
                    }
                    else
                    {
                        failureCount++;
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    logger.LogError(ex, "Failed to send broadcast to chat {ChatId}", chatId);
                }
            }

            logger.LogInformation(
                "Localized broadcast sent to {Total} users. Success: {Success}, Failed: {Failure}, English: {English}, Malayalam: {Malayalam}",
                chatIds.Count,
                successCount,
                failureCount,
                englishCount,
                malayalamCount);

            return Ok(new
            {
                totalUsers = chatIds.Count,
                successCount,
                failureCount,
                englishCount,
                malayalamCount,
                englishMessage = request.EnglishMessage,
                malayalamMessage = request.MalayalamMessage
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending localized broadcast");
            return StatusCode(500, new { error = "Failed to send broadcast" });
        }
    }
}