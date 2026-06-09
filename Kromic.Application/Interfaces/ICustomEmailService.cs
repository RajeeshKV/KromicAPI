using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface ICustomEmailService
{
    Task<CustomEmailSendResponse> SendAsync(CustomEmailRequest request, CancellationToken cancellationToken);
}
