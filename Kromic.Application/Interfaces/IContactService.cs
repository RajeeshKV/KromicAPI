using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface IContactService
{
    Task<ContactCreatedResponse> SubmitAsync(ContactSubmissionRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContactSubmissionResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ContactSubmissionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ContactSubmissionResponse?> RespondAsync(Guid id, ContactResponseRequest request, CancellationToken cancellationToken);
}
