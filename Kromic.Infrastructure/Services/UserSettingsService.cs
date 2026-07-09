using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class UserSettingsService(
    KromicDbContext dbContext,
    ILogger<UserSettingsService> logger) : IUserSettingsService
{
    public async Task<Application.Interfaces.UserSettings?> GetByChatIdAsync(string chatId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return new Application.Interfaces.UserSettings
        {
            Language = entity.Language,
            TelegramNotificationsEnabled = entity.TelegramNotificationsEnabled,
            EmailNotificationsEnabled = entity.EmailNotificationsEnabled,
            IsPaused = entity.IsPaused
        };
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
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.UserSettings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created default settings for chat ID: {ChatId}", chatId);

        return new Application.Interfaces.UserSettings
        {
            Language = entity.Language,
            TelegramNotificationsEnabled = entity.TelegramNotificationsEnabled,
            EmailNotificationsEnabled = entity.EmailNotificationsEnabled,
            IsPaused = entity.IsPaused
        };
    }

    public async Task UpdateLanguageAsync(string chatId, string language, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity == null)
        {
            entity = new Domain.Entities.UserSettings
            {
                ChatId = chatId,
                Language = language,
                TelegramNotificationsEnabled = true,
                EmailNotificationsEnabled = true,
                IsPaused = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.UserSettings.Add(entity);
        }
        else
        {
            entity.Language = language;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated language to {Language} for chat ID: {ChatId}", language, chatId);
    }

    public async Task SetTelegramNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity == null)
        {
            entity = new Domain.Entities.UserSettings
            {
                ChatId = chatId,
                Language = "en",
                TelegramNotificationsEnabled = enabled,
                EmailNotificationsEnabled = true,
                IsPaused = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.UserSettings.Add(entity);
        }
        else
        {
            entity.TelegramNotificationsEnabled = enabled;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Set Telegram notifications to {Enabled} for chat ID: {ChatId}", enabled, chatId);
    }

    public async Task SetEmailNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity == null)
        {
            entity = new Domain.Entities.UserSettings
            {
                ChatId = chatId,
                Language = "en",
                TelegramNotificationsEnabled = true,
                EmailNotificationsEnabled = enabled,
                IsPaused = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.UserSettings.Add(entity);
        }
        else
        {
            entity.EmailNotificationsEnabled = enabled;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Set email notifications to {Enabled} for chat ID: {ChatId}", enabled, chatId);
    }

    public async Task SetPausedAsync(string chatId, bool paused, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserSettings
            .FirstOrDefaultAsync(x => x.ChatId == chatId, cancellationToken);

        if (entity == null)
        {
            entity = new Domain.Entities.UserSettings
            {
                ChatId = chatId,
                Language = "en",
                TelegramNotificationsEnabled = true,
                EmailNotificationsEnabled = true,
                IsPaused = paused,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.UserSettings.Add(entity);
        }
        else
        {
            entity.IsPaused = paused;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Set paused state to {Paused} for chat ID: {ChatId}", paused, chatId);
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
}
