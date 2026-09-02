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

    [HttpPost("send")]
    public async Task<IActionResult> SendIndividualMessage([FromBody] IndividualMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChatId))
        {
            return BadRequest(new { error = "Chat ID is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        try
        {
            var user = await telegramUserService.GetUserByChatIdAsync(request.ChatId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "User not found with the specified chat ID" });
            }

            var sent = await telegramService.SendMessageToChatIdAsync(request.ChatId, request.Message, cancellationToken);
            
            if (sent)
            {
                var recipientName = string.Join(" ", new[] { user.FirstName, user.LastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x)))
                    .Trim();

                logger.LogInformation("Individual message sent to user {ChatId} ({RecipientName})", request.ChatId, recipientName);
                
                return Ok(new IndividualMessageResponse(
                    Success: true,
                    Message: "Message sent successfully",
                    ChatId: request.ChatId,
                    RecipientName: recipientName));
            }
            else
            {
                logger.LogWarning("Failed to send individual message to user {ChatId}", request.ChatId);
                return StatusCode(500, new IndividualMessageResponse(
                    Success: false,
                    Message: "Failed to send message to the user",
                    ChatId: request.ChatId,
                    RecipientName: null));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending individual message to chat {ChatId}", request.ChatId);
            return StatusCode(500, new { error = "Failed to send message" });
        }
    }

    [HttpPost("send-localized")]
    public async Task<IActionResult> SendLocalizedIndividualMessage([FromBody] LocalizedIndividualMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChatId))
        {
            return BadRequest(new { error = "Chat ID is required" });
        }

        if (string.IsNullOrWhiteSpace(request.EnglishMessage) && string.IsNullOrWhiteSpace(request.MalayalamMessage))
        {
            return BadRequest(new { error = "At least one language message is required" });
        }

        try
        {
            var user = await telegramUserService.GetUserByChatIdAsync(request.ChatId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { error = "User not found with the specified chat ID" });
            }

            var userSettings = await userSettingsService.GetByChatIdAsync(request.ChatId, cancellationToken);
            var language = userSettings?.Language ?? "en";

            var message = language == "ml"
                ? request.MalayalamMessage
                : request.EnglishMessage;

            // Fallback to English if the requested language message is empty
            if (string.IsNullOrWhiteSpace(message))
            {
                message = language == "ml" ? request.EnglishMessage : request.MalayalamMessage;
            }

            // Final fallback to English if both are empty
            if (string.IsNullOrWhiteSpace(message))
            {
                message = request.EnglishMessage;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(new { error = "No valid message available" });
            }

            var sent = await telegramService.SendMessageToChatIdAsync(request.ChatId, message, cancellationToken);

            if (sent)
            {
                var recipientName = string.Join(" ", new[] { user.FirstName, user.LastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x)))
                    .Trim();

                logger.LogInformation(
                    "Localized individual message sent to user {ChatId} ({RecipientName}) in language {Language}",
                    request.ChatId,
                    recipientName,
                    language);

                return Ok(new IndividualMessageResponse(
                    Success: true,
                    Message: "Message sent successfully",
                    ChatId: request.ChatId,
                    RecipientName: recipientName));
            }
            else
            {
                logger.LogWarning("Failed to send localized individual message to user {ChatId}", request.ChatId);
                return StatusCode(500, new IndividualMessageResponse(
                    Success: false,
                    Message: "Failed to send message to the user",
                    ChatId: request.ChatId,
                    RecipientName: null));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending localized individual message to chat {ChatId}", request.ChatId);
            return StatusCode(500, new { error = "Failed to send message" });
        }
    }
}