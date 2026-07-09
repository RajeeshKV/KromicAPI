namespace Kromic.Application.Interfaces;

public interface IUserSettingsService
{
    Task<UserSettings?> GetByChatIdAsync(string chatId, CancellationToken cancellationToken);
    Task<UserSettings> GetOrCreateAsync(string chatId, CancellationToken cancellationToken);
    Task UpdateLanguageAsync(string chatId, string language, CancellationToken cancellationToken);
    Task SetTelegramNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken);
    Task SetEmailNotificationsAsync(string chatId, bool enabled, CancellationToken cancellationToken);
    Task SetPausedAsync(string chatId, bool paused, CancellationToken cancellationToken);
    Task<bool> ShouldReceiveTelegramNotificationsAsync(string chatId, CancellationToken cancellationToken);
    Task<bool> ShouldReceiveEmailNotificationsAsync(string chatId, CancellationToken cancellationToken);
}

public sealed class UserSettings
{
    public string Language { get; set; } = "en";
    public bool TelegramNotificationsEnabled { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool IsPaused { get; set; } = false;
}
