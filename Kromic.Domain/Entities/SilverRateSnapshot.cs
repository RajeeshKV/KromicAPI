namespace Kromic.Domain.Entities;

public sealed class SilverRateSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? SourceId { get; set; }
    public decimal SilverRate { get; set; }
    public DateTimeOffset? SourceLastUpdatedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RegularEmailMessageId { get; set; }
    public string? LowestAlertMessageId { get; set; }
}