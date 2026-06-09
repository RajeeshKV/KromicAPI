namespace Kromic.Domain.Entities;

public sealed class ContactSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty;
    public string ExpectedTimeline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ContactStatus Status { get; set; } = ContactStatus.New;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? OwnerNotificationMessageId { get; set; }
    public string? ResponseText { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public string? ResponseMessageId { get; set; }
}

public enum ContactStatus
{
    New = 0,
    Responded = 1
}
