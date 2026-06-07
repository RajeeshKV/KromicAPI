using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Infrastructure.Services;

public sealed class CompanyService(KromicDbContext dbContext, IPortfolioCache cache) : ICompanyService
{
    public Task<CompanySettingsDto> GetCachedAsync(CancellationToken cancellationToken) =>
        cache.GetCompanyAsync(GetSettingsAsync, cancellationToken);

    public async Task<CompanySettingsDto> UpdateAsync(CompanySettingsDto request, CancellationToken cancellationToken)
    {
        var settings = await dbContext.CompanySettings.FirstOrDefaultAsync(cancellationToken) ?? new CompanySettings();
        settings.CompanyName = request.CompanyName;
        settings.Email = request.Email;
        settings.Phone = request.Phone;
        settings.Address = request.Address;
        settings.LinkedIn = request.LinkedIn;
        settings.Github = request.Github;
        settings.Instagram = request.Instagram;
        settings.Facebook = request.Facebook;
        settings.Twitter = request.Twitter;
        settings.YouTube = request.YouTube;
        settings.Behance = request.Behance;
        settings.Dribbble = request.Dribbble;
        settings.FooterText = request.FooterText;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        if (dbContext.Entry(settings).State == EntityState.Detached)
        {
            dbContext.CompanySettings.Add(settings);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.RemoveCompany();
        return ToDto(settings);
    }

    private async Task<CompanySettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CompanySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings is null ? new CompanySettingsDto(null, null, null, null, null, null, null, null, null, null, null, null, null) : ToDto(settings);
    }

    private static CompanySettingsDto ToDto(CompanySettings settings) =>
        new(settings.CompanyName, settings.Email, settings.Phone, settings.Address, settings.LinkedIn, settings.Github,
            settings.Instagram, settings.Facebook, settings.Twitter, settings.YouTube, settings.Behance, settings.Dribbble, settings.FooterText);
}
