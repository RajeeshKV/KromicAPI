namespace Kromic.Domain.Entities;

public sealed class CompanySettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? LinkedIn { get; set; }
    public string? Github { get; set; }
    public string? Instagram { get; set; }
    public string? Facebook { get; set; }
    public string? Twitter { get; set; }
    public string? YouTube { get; set; }
    public string? Behance { get; set; }
    public string? Dribbble { get; set; }
    public string? FooterText { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
