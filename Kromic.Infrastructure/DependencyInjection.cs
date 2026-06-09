using System.Text;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Infrastructure.Authentication;
using Kromic.Infrastructure.Cache;
using Kromic.Infrastructure.Cloudinary;
using Kromic.Infrastructure.Persistence;
using Kromic.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kromic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<CloudinaryOptions>(configuration.GetSection("Cloudinary"));
        services.Configure<BrevoOptions>(configuration.GetSection("Brevo"));

        services.AddDbContext<KromicDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddMemoryCache();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ICustomEmailService, CustomEmailService>();
        services.AddScoped<ICloudinaryImageService, CloudinaryImageService>();
        services.AddSingleton<IPortfolioCache, MemoryPortfolioCache>();
        services.AddHttpClient<ITransactionalEmailService, BrevoTransactionalEmailService>((serviceProvider, client) =>
        {
            var brevo = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BrevoOptions>>().Value;
            client.BaseAddress = new Uri(brevo.BaseUrl.TrimEnd('/') + "/");
        });

        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();
        return services;
    }
}
