namespace Kromic.Application.Interfaces;

public interface ITelegramService
{
    Task<bool> SendMessageAsync(
        string message,
        CancellationToken cancellationToken);
}
