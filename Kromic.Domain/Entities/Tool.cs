namespace Kromic.Domain.Entities;

public sealed class Tool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<ProjectTool> ProjectTools { get; set; } = [];
}
