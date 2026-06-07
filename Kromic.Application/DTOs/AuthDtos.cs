namespace Kromic.Application.DTOs;

public sealed record CreateAdminRequest(string Username, string Email, string Password);
public sealed record LoginRequest(string UsernameOrEmail, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
