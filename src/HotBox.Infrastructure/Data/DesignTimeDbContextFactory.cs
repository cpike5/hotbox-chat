using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotBox.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HotBoxDbContext>
{
    public HotBoxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HotBoxDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hotbox;Username=hotbox;Password=hotbox");

        return new HotBoxDbContext(optionsBuilder.Options);
    }
}
