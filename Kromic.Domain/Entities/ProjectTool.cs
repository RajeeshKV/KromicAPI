namespace Kromic.Domain.Entities;

public sealed class ProjectTool
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid ToolId { get; set; }
    public Tool Tool { get; set; } = null!;
}
