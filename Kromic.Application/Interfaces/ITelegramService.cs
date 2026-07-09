using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface ITelegramService
{
    Task<bool> SendMessageAsync(
        string message,
        CancellationToken cancellationToken);

    Task<bool> SendMessageToChatIdAsync(
        string chatId,
        string message,
        CancellationToken cancellationToken);

    Task<bool> SendMessageWithMenuAsync(
        string chatId,
        string message,
        List<TelegramMenuRow> menu,
        CancellationToken cancellationToken);

    Task<bool> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        bool showAlert = false,
        CancellationToken cancellationToken = default);
}
