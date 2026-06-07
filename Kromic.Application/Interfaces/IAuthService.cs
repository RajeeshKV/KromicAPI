using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface IAuthService
{
    Task CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken);
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}
