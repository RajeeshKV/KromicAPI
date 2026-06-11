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
}
