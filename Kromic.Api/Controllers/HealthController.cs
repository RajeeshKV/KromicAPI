using Kromic.Application.DTOs;
using Kromic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(KromicDbContext dbContext, IMemoryCache memoryCache) : ControllerBase
{
    [HttpGet]
    public async Task<HealthDto> Get(CancellationToken cancellationToken)
    {
        const string key = "health:db";
        if (!memoryCache.TryGetValue(key, out _))
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            memoryCache.Set(key, true, TimeSpan.FromDays(7));
        }
        return new HealthDto("Healthy", "Connected", DateTimeOffset.UtcNow);
    }

    [HttpHead]
    public async Task<IActionResult> Head(CancellationToken cancellationToken)
    {
        const string key = "health:db";
        if (!memoryCache.TryGetValue(key, out _))
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            memoryCache.Set(key, true, TimeSpan.FromDays(7));
        }
        return Ok();
    }
}