namespace Kromic.Domain.Entities;

public sealed class LocalizationResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Language { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
