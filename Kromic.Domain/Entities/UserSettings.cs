namespace Kromic.Domain.Entities;

public sealed class UserSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ChatId { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool TelegramNotificationsEnabled { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool IsPaused { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
