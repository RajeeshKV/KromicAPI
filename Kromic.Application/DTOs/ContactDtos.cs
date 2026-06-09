using System.ComponentModel.DataAnnotations;
using Kromic.Domain.Entities;

namespace Kromic.Application.DTOs;

public sealed class ContactSubmissionRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ProjectType { get; set; }

    [MaxLength(200)]
    public string ExpectedTimeline { get; set; }

    [Required, MaxLength(5000)]
    public string Description { get; set; } = string.Empty;
}

public sealed record ContactSubmissionResponse(
    Guid Id,
    string Name,
    string Email,
    string? ProjectType,
    string? ExpectedTimeline,
    string Description,
    ContactStatus Status,
    DateTimeOffset CreatedAt,
    string? ResponseText,
    DateTimeOffset? RespondedAt);

public sealed record ContactCreatedResponse(Guid Id, string Status);

public sealed class ContactResponseRequest
{
    [Required, MaxLength(5000)]
    public string ResponseText { get; set; } = string.Empty;
}
