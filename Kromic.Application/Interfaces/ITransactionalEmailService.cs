using Kromic.Domain.Entities;

namespace Kromic.Application.Interfaces;

public interface ITransactionalEmailService
{
    Task<string?> SendContactNotificationAsync(ContactSubmission submission, CancellationToken cancellationToken);
    Task<string?> SendContactResponseAsync(ContactSubmission submission, string responseText, CancellationToken cancellationToken);
}
