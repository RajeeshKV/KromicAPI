using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class UserSettingsService(
    KromicDbContext dbContext,
    IMemoryCache memoryCache,
    ILogger<UserSettingsService> logger) : IUserSettingsService
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    public async Task<Application.Interfaces.UserSettings?> GetByChatIdAsync(string chatId, CancellationToken cancellationToken)
    {
        var key = $"usersettings:{chatId}";
        if (_memoryCache.TryGetValue<Application.Interfaces.UserSettings?>(key, out var cached))
        {
            return cached;
        }
        var entity = await dbContext.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity == null)
        {
            _memoryCache.Set(key, (Application.Interfaces.UserSettings?)null, TimeSpan.FromHours(24));
            return null;
        }

        var dto = ToDto(entity);
        _memoryCache.Set(key, dto, TimeSpan.FromHours(24));
        return dto;
    }

    public async Task<Application.Interfaces.UserSettings> GetOrCreateAsync(string chatId, CancellationToken cancellationToken)
    {
        var existing = await GetByChatIdAsync(chatId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var entity = new Domain.Entities.UserSettings
        {
            ChatId = chatId,
            Language = "en",
            TelegramNotificationsEnabled = true,
            EmailNotificationsEnabled = true,
            IsPaused = false,
            GoldNotificationsEnabled = true,
            SilverNotificationsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.UserSettings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");

        logger.LogInformation("Created default settings for chat ID: {ChatId}", chatId);

        return ToDto(entity);
    }

    public async Task UpdateLanguageAsync(string chatId, string language, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateEntityAsync(chatId, cancellationToken);
        entity.Language = language;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");
        logger.LogInformation("Updated language to {Language} for chat ID: {ChatId}", language, chatId);
    }

    public async Task SetTelegramNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateEntityAsync(chatId, cancellationToken);
        entity.TelegramNotificationsEnabled = enabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");
        logger.LogInformation("Set Telegram notifications to {Enabled} for chat ID: {ChatId}", enabled, chatId);
    }

    public async Task SetEmailNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateEntityAsync(chatId, cancellationToken);
        entity.EmailNotificationsEnabled = enabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");
        logger.LogInformation("Set email notifications to {Enabled} for chat ID: {ChatId}", enabled, chatId);
    }

    public async Task SetPausedAsync(string chatId, bool paused, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateEntityAsync(chatId, cancellationToken);
        entity.IsPaused = paused;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");
        logger.LogInformation("Set paused state to {Paused} for chat ID: {ChatId}", paused, chatId);
    }

    public async Task SetGoldNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateEntityAsync(chatId, cancellationToken);
        entity.GoldNotificationsEnabled = enabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");
        logger.LogInformation("Set gold notifications to {Enabled} for chat ID: {ChatId}", enabled, chatId);
    }

    public async Task SetSilverNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateEntityAsync(chatId, cancellationToken);
        entity.SilverNotificationsEnabled = enabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove($"usersettings:{chatId}");
        logger.LogInformation("Set silver notifications to {Enabled} for chat ID: {ChatId}", enabled, chatId);
    }

    public async Task<bool> ShouldReceiveTelegramNotificationsAsync(string chatId, CancellationToken cancellationToken)
    {
        var settings = await GetByChatIdAsync(chatId, cancellationToken);
        if (settings == null)
        {
            return true; // Default to true if no settings exist
        }

        return settings.TelegramNotificationsEnabled && !settings.IsPaused;
    }

    public async Task<bool> ShouldReceiveEmailNotificationsAsync(string chatId, CancellationToken cancellationToken)
    {
        var settings = await GetByChatIdAsync(chatId, cancellationToken);
        if (settings == null)
        {
            return true; // Default to true if no settings exist
        }

        return settings.EmailNotificationsEnabled;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Domain.Entities.UserSettings> GetOrCreateEntityAsync(string chatId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity != null) return entity;

        entity = new Domain.Entities.UserSettings
        {
            ChatId = chatId,
            Language = "en",
            TelegramNotificationsEnabled = true,
            EmailNotificationsEnabled = true,
            IsPaused = false,
            GoldNotificationsEnabled = true,
            SilverNotificationsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.UserSettings.Add(entity);
        return entity;
    }

    private static Application.Interfaces.UserSettings ToDto(Domain.Entities.UserSettings entity) =>
        new()
        {
            Language = entity.Language,
            TelegramNotificationsEnabled = entity.TelegramNotificationsEnabled,
            EmailNotificationsEnabled = entity.EmailNotificationsEnabled,
            IsPaused = entity.IsPaused,
            GoldNotificationsEnabled = entity.GoldNotificationsEnabled,
            SilverNotificationsEnabled = entity.SilverNotificationsEnabled,
        };
}
