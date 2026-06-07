using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface ICompanyService
{
    Task<CompanySettingsDto> GetCachedAsync(CancellationToken cancellationToken);
    Task<CompanySettingsDto> UpdateAsync(CompanySettingsDto request, CancellationToken cancellationToken);
}
