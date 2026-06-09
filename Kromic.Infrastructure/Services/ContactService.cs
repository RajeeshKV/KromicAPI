using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Infrastructure.Services;

public sealed class ContactService(KromicDbContext dbContext, ITransactionalEmailService emailService) : IContactService
{
    public async Task<ContactCreatedResponse> SubmitAsync(ContactSubmissionRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var submission = new ContactSubmission
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            ProjectType = request.ProjectType.Trim(),
            ExpectedTimeline = request.ExpectedTimeline.Trim(),
            Description = request.Description.Trim(),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        dbContext.ContactSubmissions.Add(submission);
        await dbContext.SaveChangesAsync(cancellationToken);

        submission.OwnerNotificationMessageId = await emailService.SendContactNotificationAsync(submission, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ContactCreatedResponse(submission.Id, "Received");
    }

    public async Task<IReadOnlyList<ContactSubmissionResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.ContactSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContactSubmissionResponse(
                x.Id,
                x.Name,
                x.Email,
                x.ProjectType,
                x.ExpectedTimeline,
                x.Description,
                x.Status,
                x.CreatedAt,
                x.ResponseText,
                x.RespondedAt))
            .ToListAsync(cancellationToken);

    public async Task<ContactSubmissionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ContactSubmissions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ContactSubmissionResponse(
                x.Id,
                x.Name,
                x.Email,
                x.ProjectType,
                x.ExpectedTimeline,
                x.Description,
                x.Status,
                x.CreatedAt,
                x.ResponseText,
                x.RespondedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ContactSubmissionResponse?> RespondAsync(Guid id, ContactResponseRequest request, CancellationToken cancellationToken)
    {
        var submission = await dbContext.ContactSubmissions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (submission is null)
        {
            return null;
        }

        var responseText = request.ResponseText.Trim();
        submission.ResponseMessageId = await emailService.SendContactResponseAsync(submission, responseText, cancellationToken);
        submission.ResponseText = responseText;
        submission.RespondedAt = DateTimeOffset.UtcNow;
        submission.Status = ContactStatus.Responded;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(submission);
    }

    private static ContactSubmissionResponse ToResponse(ContactSubmission submission) =>
        new(
            submission.Id,
            submission.Name,
            submission.Email,
            submission.ProjectType,
            submission.ExpectedTimeline,
            submission.Description,
            submission.Status,
            submission.CreatedAt,
            submission.ResponseText,
            submission.RespondedAt);
}
