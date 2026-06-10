namespace Kromic.Domain.Entities;

public sealed class GoldRateSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? SourceId { get; set; }
    public decimal R22KT { get; set; }
    public bool R22KTShow { get; set; }
    public decimal? R18KT { get; set; }
    public decimal? R24KT { get; set; }
    public DateTimeOffset? SourceLastUpdatedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsLowestAtFetch { get; set; }
    public string? RegularEmailMessageId { get; set; }
    public string? LowestAlertMessageId { get; set; }
}
