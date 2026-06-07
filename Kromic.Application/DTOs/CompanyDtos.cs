namespace Kromic.Application.DTOs;

public sealed record CompanySettingsDto(
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Address,
    string? LinkedIn,
    string? Github,
    string? Instagram,
    string? Facebook,
    string? Twitter,
    string? YouTube,
    string? Behance,
    string? Dribbble,
    string? FooterText);
