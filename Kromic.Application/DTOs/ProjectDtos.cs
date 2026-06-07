using Microsoft.AspNetCore.Http;

namespace Kromic.Application.DTOs;

public sealed record ProjectImageResponse(Guid Id, string ImageUrl, string CloudinaryPublicId, int DisplayOrder);
public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? WebsiteUrl,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<string> Tools,
    IReadOnlyList<ProjectImageResponse> Images);

public sealed class UpsertProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Tools { get; set; } = [];
    public List<IFormFile> Images { get; set; } = [];
}
