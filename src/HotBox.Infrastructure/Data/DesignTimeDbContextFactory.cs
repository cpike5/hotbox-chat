using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotBox.Infrastructure.Data;

/// <summary>
/// Design-time factory used by dotnet-ef to scaffold migrations targeting PostgreSQL.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HotBoxDbContext>
{
    public HotBoxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HotBoxDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hotbox_design;Username=hotbox;Password=design");
        return new HotBoxDbContext(optionsBuilder.Options);
    }
}
