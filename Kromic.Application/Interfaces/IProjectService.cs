using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> GetAllCachedAsync(CancellationToken cancellationToken);
    Task<ProjectResponse?> GetBySlugCachedAsync(string slug, CancellationToken cancellationToken);
    Task<ProjectResponse> CreateAsync(UpsertProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectResponse?> UpdateAsync(Guid id, UpsertProjectRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
