using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kromic.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KromicDbContext>
{
    public KromicDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KromicDbContext>();
        
        // Use environment variable for connection string at design time
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
            ?? "Host=localhost;Database=kromic;Username=postgres;Password=postgres";
        
        optionsBuilder.UseNpgsql(connectionString);
        
        return new KromicDbContext(optionsBuilder.Options);
    }
}
