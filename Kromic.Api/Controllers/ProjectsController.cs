using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
public sealed class ProjectsController(IProjectService projectService) : ControllerBase
{
    [HttpGet("api/projects")]
    public Task<IReadOnlyList<ProjectResponse>> GetAll(CancellationToken cancellationToken) =>
        projectService.GetAllCachedAsync(cancellationToken);

    [HttpGet("api/projects/{slug}")]
    public async Task<ActionResult<ProjectResponse>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var project = await projectService.GetBySlugCachedAsync(slug, cancellationToken);
        return project is null ? NotFound() : project;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/projects")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProjectResponse>> Create([FromForm] UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBySlug), new { slug = project.Slug }, project);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("api/admin/projects/{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProjectResponse>> Update(Guid id, [FromForm] UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectService.UpdateAsync(id, request, cancellationToken);
        return project is null ? NotFound() : project;
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("api/admin/projects/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await projectService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
