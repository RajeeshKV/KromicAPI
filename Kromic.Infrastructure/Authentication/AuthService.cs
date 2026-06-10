using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kromic.Infrastructure.Authentication;

public sealed class AuthService(
    KromicDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IGoldRateService goldRateService,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken)
    {
        if (await dbContext.AdminUsers.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Admin user already exists.");
        }

        var admin = new AdminUser
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var lookup = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var admin = await dbContext.AdminUsers.SingleOrDefaultAsync(
            x => x.Username.ToLower() == lookup || x.Email.ToLower() == lookup,
            cancellationToken);

        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username/email or password.");
        }

        var goldRate = await RefreshGoldRateForLoginAsync(cancellationToken);
        return await IssueTokenPairAsync(admin, goldRate, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var token = await dbContext.RefreshTokens
            .Include(x => x.AdminUser)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (token is null || !token.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        var response = await IssueTokenPairAsync(token.AdminUser, goldRate: null, cancellationToken);
        token.ReplacedByTokenHash = HashToken(response.RefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<TokenResponse> IssueTokenPairAsync(
        AdminUser admin,
        GoldRateSnapshotResponse? goldRate,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var accessToken = CreateAccessToken(admin, expiresAt);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            AdminUserId = admin.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TokenResponse(accessToken, refreshToken, _jwt.AccessTokenMinutes * 60, goldRate);
    }

    private async Task<GoldRateSnapshotResponse?> RefreshGoldRateForLoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await goldRateService.FetchAndStoreAsync(
                sendRegularEmail: false,
                sendLowestAlert: true,
                cancellationToken);

            return result.Snapshot;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gold rate refresh during admin login failed.");
            return await goldRateService.GetCurrentAsync(cancellationToken);
        }
    }

    private string CreateAccessToken(AdminUser admin, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
