using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Kromic.Infrastructure.Cache;

public sealed class MemoryPortfolioCache(IMemoryCache cache) : IPortfolioCache
{
    private const string ProjectsAllKey = "Projects_All";
    private const string CompanySettingsKey = "Company_Settings";
    private static string ProjectKey(string slug) => $"Project_{slug.ToLowerInvariant()}";

    public Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(Func<CancellationToken, Task<IReadOnlyList<ProjectResponse>>> factory, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ProjectsAllKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return factory(cancellationToken);
        })!;

    public Task<ProjectResponse?> GetProjectAsync(string slug, Func<CancellationToken, Task<ProjectResponse?>> factory, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ProjectKey(slug), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return factory(cancellationToken);
        });

    public Task<CompanySettingsDto> GetCompanyAsync(Func<CancellationToken, Task<CompanySettingsDto>> factory, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(CompanySettingsKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return factory(cancellationToken);
        })!;

    public void RemoveProjects()
    {
        cache.Remove(ProjectsAllKey);
    }

    public void RemoveCompany()
    {
        cache.Remove(CompanySettingsKey);
    }
}
