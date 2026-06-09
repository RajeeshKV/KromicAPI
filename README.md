# Kromic API

.NET 8 Clean Architecture backend for the Kromic Agency portfolio website.

## Projects

- `Kromic.Domain` - entities.
- `Kromic.Application` - DTOs and contracts.
- `Kromic.Infrastructure` - EF Core, PostgreSQL, JWT, refresh tokens, Cloudinary, cache.
- `Kromic.Api` - controllers, middleware, startup, Swagger.

## Required Environment Variables

```text
ASPNETCORE_ENVIRONMENT
ASPNETCORE_URLS
ConnectionStrings__DefaultConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
Jwt__AccessTokenMinutes
Jwt__RefreshTokenDays
Cloudinary__CloudName
Cloudinary__ApiKey
Cloudinary__ApiSecret
Cloudinary__Folder
Brevo__BaseUrl
Brevo__ApiKey
Brevo__SenderEmail
Brevo__SenderName
Brevo__OwnerEmail
Brevo__OwnerName
Brevo__ContactNotificationTemplateId
Brevo__ContactResponseTemplateId
Brevo__CustomEmailTemplateId
Cors__AllowedOrigins
```

## Run Locally

```powershell
dotnet restore
dotnet run --project Kromic.Api/Kromic.Api.csproj
```

The API applies EF Core migrations on startup.

## Render

Render deployment files are included:

- `Dockerfile` builds and runs the API on port `8080`.
- `render.yaml` defines the Docker web service and health check.
- `EFMigration.sh` runs EF Core migrations during Render pre-deploy.

Configure the `sync: false` variables in Render before the first deploy.

Set `Cors__AllowedOrigins` as a comma-separated list when more than one frontend URL is allowed:

```text
https://kromic.com,https://www.kromic.com,https://admin.kromic.com
```
