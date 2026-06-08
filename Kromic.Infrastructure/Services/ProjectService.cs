using System.Text.RegularExpressions;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Infrastructure.Services;

public sealed partial class ProjectService(
    KromicDbContext dbContext,
    ICloudinaryImageService imageService,
    IPortfolioCache cache) : IProjectService
{
    public Task<IReadOnlyList<ProjectResponse>> GetAllCachedAsync(CancellationToken cancellationToken) =>
        cache.GetProjectsAsync(async _ => await QueryProjects().ToListAsync(cancellationToken), cancellationToken);

    public async Task<ProjectResponse?> GetBySlugCachedAsync(string slug, CancellationToken cancellationToken)
    {
        var projects = await GetAllCachedAsync(cancellationToken);
        return projects.SingleOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProjectResponse> CreateAsync(UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        var project = new Project
        {
            Name = request.Name.Trim(),
            Slug = await CreateUniqueSlugAsync(request.Name, null, cancellationToken),
            Description = request.Description.Trim(),
            WebsiteUrl = request.WebsiteUrl,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };

        await ApplyToolsAsync(project, request.Tools, cancellationToken);
        await AddImagesAsync(project, request.Images, cancellationToken);

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.RemoveProjects();
        return await QueryProjectById(project.Id).SingleAsync(cancellationToken);
    }

    public async Task<ProjectResponse?> UpdateAsync(Guid id, UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(x => x.ProjectTools)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (project is null)
        {
            return null;
        }

        project.Name = request.Name.Trim();
        project.Slug = await CreateUniqueSlugAsync(request.Name, id, cancellationToken);
        project.Description = request.Description.Trim();
        project.WebsiteUrl = request.WebsiteUrl;
        project.DisplayOrder = request.DisplayOrder;
        project.IsActive = request.IsActive;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.ProjectTools.RemoveRange(project.ProjectTools);
        await ApplyToolsAsync(project, request.Tools, cancellationToken);

        var replacedCloudinaryIds = Array.Empty<string>();
        if (request.Images.Count > 0)
        {
            replacedCloudinaryIds = await ReplaceImagesAsync(project.Id, request.Images, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var publicId in replacedCloudinaryIds)
        {
            await imageService.DeleteAsync(publicId, cancellationToken);
        }

        cache.RemoveProjects();
        return await QueryProjectById(project.Id).SingleAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project is null)
        {
            return false;
        }

        foreach (var image in project.Images)
        {
            await imageService.DeleteAsync(image.CloudinaryPublicId, cancellationToken);
        }

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.RemoveProjects();
        return true;
    }

    private IQueryable<ProjectResponse> QueryProjects() =>
        ToProjectResponseQuery(dbContext.Projects
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAt));

    private IQueryable<ProjectResponse> QueryProjectById(Guid id) =>
        ToProjectResponseQuery(dbContext.Projects
            .AsNoTracking()
            .Where(x => x.Id == id));

    private static IQueryable<ProjectResponse> ToProjectResponseQuery(IQueryable<Project> query) =>
        query
            .Select(x => new ProjectResponse(
                x.Id,
                x.Name,
                x.Slug,
                x.Description,
                x.WebsiteUrl,
                x.DisplayOrder,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt,
                x.ProjectTools.OrderBy(pt => pt.Tool.Name).Select(pt => pt.Tool.Name).ToList(),
                x.Images.OrderBy(i => i.DisplayOrder).Select(i => new ProjectImageResponse(i.Id, i.ImageUrl, i.CloudinaryPublicId, i.DisplayOrder)).ToList()));

    private async Task ApplyToolsAsync(Project project, IEnumerable<string> toolNames, CancellationToken cancellationToken)
    {
        var normalized = toolNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var name in normalized)
        {
            var tool = await dbContext.Tools.SingleOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), cancellationToken)
                ?? new Domain.Entities.Tool { Name = name };
            project.ProjectTools.Add(new ProjectTool { Project = project, Tool = tool });
        }
    }

    private async Task AddImagesAsync(Project project, IReadOnlyList<Microsoft.AspNetCore.Http.IFormFile> images, CancellationToken cancellationToken)
    {
        for (var i = 0; i < images.Count; i++)
        {
            var upload = await imageService.UploadAsync(images[i], cancellationToken);
            project.Images.Add(new ProjectImage
            {
                CloudinaryPublicId = upload.PublicId,
                ImageUrl = upload.Url,
                DisplayOrder = i
            });
        }
    }

    private async Task<string[]> ReplaceImagesAsync(Guid projectId, IReadOnlyList<Microsoft.AspNetCore.Http.IFormFile> images, CancellationToken cancellationToken)
    {
        var existingImages = await dbContext.ProjectImages
            .Where(x => x.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var uploadedImages = new List<ProjectImage>();

        try
        {
            for (var i = 0; i < images.Count; i++)
            {
                var upload = await imageService.UploadAsync(images[i], cancellationToken);
                uploadedImages.Add(new ProjectImage
                {
                    ProjectId = projectId,
                    CloudinaryPublicId = upload.PublicId,
                    ImageUrl = upload.Url,
                    DisplayOrder = i
                });
            }

            dbContext.ProjectImages.RemoveRange(existingImages);
            dbContext.ProjectImages.AddRange(uploadedImages);
            return existingImages.Select(x => x.CloudinaryPublicId).ToArray();
        }
        catch
        {
            foreach (var uploadedImage in uploadedImages)
            {
                await imageService.DeleteAsync(uploadedImage.CloudinaryPublicId, cancellationToken);
            }

            throw;
        }
    }

    private async Task<string> CreateUniqueSlugAsync(string name, Guid? currentProjectId, CancellationToken cancellationToken)
    {
        var baseSlug = SlugRegex().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = Guid.NewGuid().ToString("N")[..8];
        }

        var slug = baseSlug;
        var suffix = 1;
        while (await dbContext.Projects.AnyAsync(x => x.Slug == slug && x.Id != currentProjectId, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
