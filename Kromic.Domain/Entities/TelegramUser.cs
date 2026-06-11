namespace Kromic.Domain.Entities;

public sealed class TelegramUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ChatId { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastInteractedAt { get; set; }
}
