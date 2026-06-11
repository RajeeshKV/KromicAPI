using Kromic.Domain.Entities;

namespace Kromic.Application.Interfaces;

public interface ITelegramUserService
{
    Task<TelegramUser?> GetUserByChatIdAsync(string chatId, CancellationToken cancellationToken);
    Task<TelegramUser> AddOrUpdateUserAsync(
        string chatId,
        string? firstName,
        string? lastName,
        string? username,
        CancellationToken cancellationToken);
    Task<List<string>> GetActiveChatIdsAsync(CancellationToken cancellationToken);
    Task<int> GetActiveChatCountAsync(CancellationToken cancellationToken);
}
