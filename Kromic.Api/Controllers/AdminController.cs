using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(IAuthService authService) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create(CreateAdminRequest request, CancellationToken cancellationToken)
    {
        await authService.CreateAdminAsync(request, cancellationToken);
        return Created("", new { message = "Admin created." });
    }
}
