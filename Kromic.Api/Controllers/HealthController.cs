using Kromic.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public HealthDto Get() => new("Healthy", "Connected", DateTimeOffset.UtcNow);

    [HttpHead]
    public IActionResult Head() => Ok();
}
