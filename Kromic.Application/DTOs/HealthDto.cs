namespace Kromic.Application.DTOs;

public sealed record HealthDto(string Status, string Database, DateTimeOffset ServerTime);
