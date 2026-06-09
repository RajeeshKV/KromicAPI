using System.ComponentModel.DataAnnotations;

namespace Kromic.Application.DTOs;

public sealed class CustomEmailRecipientRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Name { get; set; }
}

public sealed class CustomEmailRequest
{
    public bool IncludeAllContactedUsers { get; set; }

    public List<Guid> ContactIds { get; set; } = [];

    public List<CustomEmailRecipientRequest> Recipients { get; set; } = [];

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Heading { get; set; }

    [Required, MaxLength(10000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CallToActionText { get; set; }

    [Url, MaxLength(2048)]
    public string? CallToActionUrl { get; set; }
}

public sealed record CustomEmailRecipientResponse(string Email, string Name, string? MessageId);

public sealed record CustomEmailSendResponse(int SentCount, IReadOnlyList<CustomEmailRecipientResponse> Recipients);
