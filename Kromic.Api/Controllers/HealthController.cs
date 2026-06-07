using Kromic.Application.DTOs;
using Kromic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(KromicDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<HealthDto> Get(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        return new HealthDto("Healthy", "Connected", DateTimeOffset.UtcNow);
    }

    [HttpHead]
    public async Task<IActionResult> Head(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        return Ok();
    }
}
