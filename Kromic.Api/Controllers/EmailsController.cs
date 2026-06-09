using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/emails")]
public sealed class EmailsController(ICustomEmailService customEmailService) : ControllerBase
{
    [HttpPost("custom")]
    public Task<CustomEmailSendResponse> SendCustom(CustomEmailRequest request, CancellationToken cancellationToken) =>
        customEmailService.SendAsync(request, cancellationToken);
}
