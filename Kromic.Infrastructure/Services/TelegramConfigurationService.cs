using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class TelegramConfigurationService(
    KromicDbContext dbContext,
    IOptions<GoldRateOptions> options,
    ILogger<TelegramConfigurationService> logger) : ITelegramConfigurationService
{
    private readonly GoldRateOptions _options = options.Value;
    private const string ConfigVersionKey = "TelegramConfigurationVersion";

    public async Task ApplyConfigurationAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            logger.LogWarning("Telegram bot token is not configured. Skipping Telegram configuration.");
            return;
        }

        try
        {
            var currentHash = CalculateConfigurationHash();
            var storedVersion = await GetStoredConfigurationVersionAsync(cancellationToken);

            if (currentHash == storedVersion)
            {
                logger.LogInformation("Telegram configuration is up to date (version: {Hash})", currentHash);
                return;
            }

            logger.LogInformation("Applying new Telegram configuration (old: {OldHash}, new: {NewHash})", 
                storedVersion ?? "none", currentHash);

            await SetMyCommandsAsync(cancellationToken);
            await SetMyDescriptionAsync(cancellationToken);
            await SetMyShortDescriptionAsync(cancellationToken);
            await SetChatMenuButtonAsync(cancellationToken);

            await SaveConfigurationVersionAsync(currentHash, cancellationToken);
            logger.LogInformation("Telegram configuration applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply Telegram configuration");
        }
    }

    private string CalculateConfigurationHash()
    {
        var commands = GetBotCommands();
        var commandDefinitions = string.Join("|", commands.Select(c => $"{c.Command}:{c.Description}"));
        var bytes = Encoding.UTF8.GetBytes(commandDefinitions);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16]; // Use first 16 characters
    }

    private async Task<string?> GetStoredConfigurationVersionAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.ApplicationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == ConfigVersionKey, cancellationToken);
        return setting?.Value;
    }

    private async Task SaveConfigurationVersionAsync(string version, CancellationToken cancellationToken)
    {
        var setting = await dbContext.ApplicationSettings
            .FirstOrDefaultAsync(x => x.Key == ConfigVersionKey, cancellationToken);

        if (setting == null)
        {
            setting = new Domain.Entities.ApplicationSettings
            {
                Key = ConfigVersionKey,
                Value = version,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.ApplicationSettings.Add(setting);
        }
        else
        {
            setting.Value = version;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SetMyCommandsAsync(CancellationToken cancellationToken)
    {
        var commands = GetBotCommands();
        var payload = new
        {
            commands = commands.Select(c => new
            {
                command = c.Command,
                description = c.Description
            }).ToArray(),
            language_code = "en"
        };

        var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/setMyCommands";
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to set MyCommands: {errorContent}");
        }

        logger.LogInformation("Set Telegram bot commands successfully");
    }

    private async Task SetMyDescriptionAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            description = "Get daily gold rate updates and historical data for 22K gold in Kerala, India.",
            language_code = "en"
        };

        var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/setMyDescription";
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Failed to set MyDescription: {Error}", errorContent);
        }
        else
        {
            logger.LogInformation("Set Telegram bot description successfully");
        }
    }

    private async Task SetMyShortDescriptionAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            short_description = "Daily 22K gold rate updates for Kerala",
            language_code = "en"
        };

        var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/setMyShortDescription";
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Failed to set MyShortDescription: {Error}", errorContent);
        }
        else
        {
            logger.LogInformation("Set Telegram bot short description successfully");
        }
    }

    private async Task SetChatMenuButtonAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            menu_button = new
            {
                type = "commands"
            }
        };

        var url = $"https://api.telegram.org/bot{_options.TelegramBotToken}/setChatMenuButton";
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Failed to set ChatMenuButton: {Error}", errorContent);
        }
        else
        {
            logger.LogInformation("Set Telegram chat menu button successfully");
        }
    }

    private List<(string Command, string Description)> GetBotCommands()
    {
        return new List<(string, string)>
        {
            ("start", "Start the bot and get current rate"),
            ("currentrate", "Get current gold rate"),
            ("lastonemonthrates", "View last 30 days rates"),
            ("highestlowest", "View highest & lowest rates (30 days)"),
            ("history", "View historical rates by date"),
            ("emailalerts", "Subscribe to email alerts"),
            ("unsubscribeemail", "Unsubscribe from email alerts"),
            ("settings", "Manage notification settings"),
            ("pause", "Pause Telegram notifications"),
            ("resume", "Resume Telegram notifications"),
            ("feedback", "Send feedback to admin"),
            ("help", "Show available commands")
        };
    }
}
