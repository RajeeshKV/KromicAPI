using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class TelegramUserService(
    KromicDbContext dbContext,
    ILogger<TelegramUserService> logger) : ITelegramUserService
{
    public async Task<TelegramUser?> GetUserByChatIdAsync(string chatId, CancellationToken cancellationToken)
    {
        return await dbContext.TelegramUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);
    }

    public async Task<TelegramUser> AddOrUpdateUserAsync(
        string chatId,
        string? firstName,
        string? lastName,
        string? username,
        CancellationToken cancellationToken)
    {
        var existingUser = await dbContext.TelegramUsers
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (existingUser != null)
        {
            existingUser.FirstName = firstName ?? existingUser.FirstName;
            existingUser.LastName = lastName ?? existingUser.LastName;
            existingUser.Username = username ?? existingUser.Username;
            existingUser.IsActive = true;
            existingUser.LastInteractedAt = DateTimeOffset.UtcNow;

            dbContext.TelegramUsers.Update(existingUser);
            logger.LogInformation("Updated existing Telegram user with chat ID: {ChatId}", chatId);
        }
        else
        {
            var newUser = new TelegramUser
            {
                ChatId = chatId,
                FirstName = firstName,
                LastName = lastName,
                Username = username,
                IsActive = true,
                LastInteractedAt = DateTimeOffset.UtcNow
            };

            dbContext.TelegramUsers.Add(newUser);
            logger.LogInformation("Added new Telegram user with chat ID: {ChatId}", chatId);
            existingUser = newUser;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existingUser;
    }

    public async Task<List<string>> GetActiveChatIdsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.TelegramUsers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ChatId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetActiveChatCountAsync(CancellationToken cancellationToken)
    {
        return await dbContext.TelegramUsers
            .AsNoTracking()
            .CountAsync(x => x.IsActive, cancellationToken);
    }
}
