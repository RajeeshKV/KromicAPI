using Kromic.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kromic.Infrastructure.Persistence;

public sealed class KromicDbContext(DbContextOptions<KromicDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();
    public DbSet<Tool> Tools => Set<Tool>();
    public DbSet<ProjectTool> ProjectTools => Set<ProjectTool>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(100);
            entity.Property(x => x.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.AdminUser).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.AdminUserId);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Slug).HasMaxLength(220);
            entity.HasMany(x => x.Images).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tool>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<ProjectTool>().HasKey(x => new { x.ProjectId, x.ToolId });
        modelBuilder.Entity<ProjectTool>().HasOne(x => x.Project).WithMany(x => x.ProjectTools).HasForeignKey(x => x.ProjectId);
        modelBuilder.Entity<ProjectTool>().HasOne(x => x.Tool).WithMany(x => x.ProjectTools).HasForeignKey(x => x.ToolId);
    }
}
