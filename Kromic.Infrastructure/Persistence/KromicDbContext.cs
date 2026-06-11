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
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<GoldRateSnapshot> GoldRateSnapshots => Set<GoldRateSnapshot>();
    public DbSet<TelegramUser> TelegramUsers => Set<TelegramUser>();

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

        modelBuilder.Entity<ContactSubmission>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.ProjectType).HasMaxLength(50);
            entity.Property(x => x.ExpectedTimeline).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(5000);
            entity.Property(x => x.ResponseText).HasMaxLength(5000);
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.OwnerNotificationMessageId).HasMaxLength(200);
            entity.Property(x => x.ResponseMessageId).HasMaxLength(200);
        });

        modelBuilder.Entity<GoldRateSnapshot>(entity =>
        {
            entity.HasIndex(x => x.FetchedAt);
            entity.HasIndex(x => x.R22KT);
            entity.Property(x => x.R22KT).HasPrecision(18, 2);
            entity.Property(x => x.R18KT).HasPrecision(18, 2);
            entity.Property(x => x.R24KT).HasPrecision(18, 2);
            entity.Property(x => x.RegularEmailMessageId).HasMaxLength(200);
            entity.Property(x => x.LowestAlertMessageId).HasMaxLength(200);
        });

        modelBuilder.Entity<TelegramUser>(entity =>
        {
            entity.HasIndex(x => x.ChatId).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.IsActive);
            entity.Property(x => x.ChatId).HasMaxLength(50);
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.Username).HasMaxLength(100);
        });
    }
}
