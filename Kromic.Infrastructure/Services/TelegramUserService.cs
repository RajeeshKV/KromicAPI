using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class TelegramUserService(
    KromicDbContext dbContext,
    IMemoryCache memoryCache,
    ILogger<TelegramUserService> logger) : ITelegramUserService
{
    private const string ActiveChatIdsCacheKey = "telegram:activechatids";
    private const string ActiveChatCountCacheKey = "telegram:activechatcount";

    private readonly IMemoryCache _memoryCache = memoryCache;
    public async Task<TelegramUser?> GetUserByChatIdAsync(string chatId, CancellationToken cancellationToken)
    {
        var key = $"telegramuser:{chatId}";
        if (_memoryCache.TryGetValue<TelegramUser?>(key, out var cached))
        {
            return cached;
        }
        var entity = await dbContext.TelegramUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);
        _memoryCache.Set(key, entity);
        return entity;
    }

    public async Task<TelegramUser> AddOrUpdateUserAsync(
        string chatId,
        string? firstName,
        string? lastName,
        string? username,
        CancellationToken cancellationToken,
        bool updateLastInteractedAt = true)
    {
        var existingUser = await dbContext.TelegramUsers
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (existingUser != null)
        {
            existingUser.FirstName = firstName ?? existingUser.FirstName;
            existingUser.LastName = lastName ?? existingUser.LastName;
            existingUser.Username = username ?? existingUser.Username;
            existingUser.IsActive = true;
            if (updateLastInteractedAt)
            {
                existingUser.LastInteractedAt = DateTimeOffset.UtcNow;
            }

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
                LastInteractedAt = updateLastInteractedAt ? DateTimeOffset.UtcNow : null
            };

            dbContext.TelegramUsers.Add(newUser);
            logger.LogInformation("Added new Telegram user with chat ID: {ChatId}", chatId);
            existingUser = newUser;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"telegramuser:{chatId}");
        InvalidateActiveUserCache();
        return existingUser;
    }

    public async Task<List<string>> GetActiveChatIdsAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue<List<string>>(ActiveChatIdsCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var chatIds = await dbContext.TelegramUsers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.ChatId)
            .ToListAsync(cancellationToken);

        _memoryCache.Set(ActiveChatIdsCacheKey, chatIds);

        if (chatIds.Count == 0)
        {
            logger.LogWarning(
                "Resolved 0 active Telegram chat IDs from database. No rate notifications will be delivered until a user starts the bot or TelegramChatIds is configured. Cached until a user is added.");
        }
        else
        {
            logger.LogDebug(
                "Resolved {ChatIdCount} active Telegram chat IDs from database (cached until a user is added or removed).",
                chatIds.Count);
        }

        return chatIds;
    }

    public async Task<int> GetActiveChatCountAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue<int>(ActiveChatCountCacheKey, out var cached))
        {
            return cached;
        }

        var count = await dbContext.TelegramUsers
            .AsNoTracking()
            .CountAsync(x => x.IsActive, cancellationToken);

        _memoryCache.Set(ActiveChatCountCacheKey, count);
        return count;
    }

    private void InvalidateActiveUserCache()
    {
        _memoryCache.Remove(ActiveChatIdsCacheKey);
        _memoryCache.Remove(ActiveChatCountCacheKey);
    }
    public async Task<IReadOnlyList<TelegramBotUserResponse>> GetUsersWithEmailSubscriptionsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.TelegramUsers
            .AsNoTracking()
            .GroupJoin(
                dbContext.GoldRateEmailSubscriptions.AsNoTracking(),
                user => user.ChatId,
                subscription => subscription.ChatId,
                (user, subscriptions) => new { user, subscription = subscriptions.OrderByDescending(x => x.UpdatedAt).FirstOrDefault() })
            .OrderByDescending(x => x.user.LastInteractedAt ?? x.user.CreatedAt)
            .Select(x => new TelegramBotUserResponse(
                x.user.Id,
                x.user.ChatId,
                x.user.FirstName,
                x.user.LastName,
                x.user.Username,
                x.user.IsActive,
                x.user.CreatedAt,
                x.user.LastInteractedAt,
                x.subscription == null ? null : x.subscription.Email,
                x.subscription != null && x.subscription.IsActive && x.subscription.Email != null,
                x.subscription == null ? null : x.subscription.SubscribedAt,
                x.subscription == null ? null : x.subscription.UnsubscribedAt))
            .ToListAsync(cancellationToken);
    }
}
