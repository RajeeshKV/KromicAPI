using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface IPortfolioCache
{
    Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(Func<CancellationToken, Task<IReadOnlyList<ProjectResponse>>> factory, CancellationToken cancellationToken);
    Task<ProjectResponse?> GetProjectAsync(string slug, Func<CancellationToken, Task<ProjectResponse?>> factory, CancellationToken cancellationToken);
    Task<CompanySettingsDto> GetCompanyAsync(Func<CancellationToken, Task<CompanySettingsDto>> factory, CancellationToken cancellationToken);
    void RemoveProjects();
    void RemoveCompany();
}
