using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;
using Kromic.Api.Middleware;
using Kromic.Infrastructure;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});
builder.Services.AddInfrastructure(builder.Configuration);

var allowedOrigins = GetAllowedOrigins(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<KromicDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase))
{
    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();


app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/api/health"));
app.MapControllers();

app.Run();

static string[] GetAllowedOrigins(IConfiguration configuration)
{
    var indexedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var csvOrigins = configuration["Cors:AllowedOriginsCsv"] ?? configuration["Cors:AllowedOrigins"];

    var origins = indexedOrigins
        .Concat((csvOrigins ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return origins.Length > 0 ? origins : ["http://localhost:3000"];
}
