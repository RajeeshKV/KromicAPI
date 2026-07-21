using Kromic.Application.DTOs;
using Kromic.Domain.Entities;

namespace Kromic.Application.Interfaces;

public interface ITelegramUserService
{
    Task<Kromic.Domain.Entities.TelegramUser?> GetUserByChatIdAsync(string chatId, CancellationToken cancellationToken);
    Task<Kromic.Domain.Entities.TelegramUser> AddOrUpdateUserAsync(
        string chatId,
        string? firstName,
        string? lastName,
        string? username,
        CancellationToken cancellationToken,
        bool updateLastInteractedAt = true);
    Task<List<string>> GetActiveChatIdsAsync(CancellationToken cancellationToken);
    Task<int> GetActiveChatCountAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TelegramBotUserResponse>> GetUsersWithEmailSubscriptionsAsync(CancellationToken cancellationToken);
}
