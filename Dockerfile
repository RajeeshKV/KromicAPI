FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["KromicAPI.sln", "."]
COPY ["Kromic.Domain/Kromic.Domain.csproj", "Kromic.Domain/"]
COPY ["Kromic.Application/Kromic.Application.csproj", "Kromic.Application/"]
COPY ["Kromic.Infrastructure/Kromic.Infrastructure.csproj", "Kromic.Infrastructure/"]
COPY ["Kromic.Api/Kromic.Api.csproj", "Kromic.Api/"]
RUN dotnet restore "KromicAPI.sln"
COPY . .
RUN dotnet publish "Kromic.Api/Kromic.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY ["EFMigration.sh", "./EFMigration.sh"]
ENTRYPOINT ["dotnet", "Kromic.Api.dll"]
