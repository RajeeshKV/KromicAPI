namespace Kromic.Domain.Entities;

public sealed class GoldRateEmailSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ChatId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public string UnsubscribeToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? PendingRequestedAt { get; set; }
    public DateTimeOffset? PendingExpiresAt { get; set; }
    public DateTimeOffset? SubscribedAt { get; set; }
    public DateTimeOffset? UnsubscribedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
