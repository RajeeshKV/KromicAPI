using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
public sealed class CompanyController(ICompanyService companyService) : ControllerBase
{
    [HttpGet("api/company")]
    public Task<CompanySettingsDto> Get(CancellationToken cancellationToken) =>
        companyService.GetCachedAsync(cancellationToken);

    [Authorize(Roles = "Admin")]
    [HttpPut("api/admin/company")]
    public Task<CompanySettingsDto> Update(CompanySettingsDto request, CancellationToken cancellationToken) =>
        companyService.UpdateAsync(request, cancellationToken);
}
