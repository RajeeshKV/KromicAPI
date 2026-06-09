using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
public sealed class ContactController(IContactService contactService) : ControllerBase
{
    [HttpPost("api/contact")]
    public Task<ContactCreatedResponse> Submit(ContactSubmissionRequest request, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        return contactService.SubmitAsync(request, ipAddress, userAgent, cancellationToken);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/contacts")]
    public Task<IReadOnlyList<ContactSubmissionResponse>> GetAll(CancellationToken cancellationToken) =>
        contactService.GetAllAsync(cancellationToken);

    [Authorize(Roles = "Admin")]
    [HttpGet("api/admin/contacts/{id:guid}")]
    public async Task<ActionResult<ContactSubmissionResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var submission = await contactService.GetByIdAsync(id, cancellationToken);
        return submission is null ? NotFound() : submission;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/contacts/{id:guid}/respond")]
    public async Task<ActionResult<ContactSubmissionResponse>> Respond(Guid id, ContactResponseRequest request, CancellationToken cancellationToken)
    {
        var submission = await contactService.RespondAsync(id, request, cancellationToken);
        return submission is null ? NotFound() : submission;
    }
}
