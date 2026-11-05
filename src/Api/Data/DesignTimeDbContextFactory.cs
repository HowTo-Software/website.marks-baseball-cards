using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MarksBaseballCards.Api.Data;

/// <summary>
/// Used by the EF Core CLI (`dotnet ef`) to build the context at design time.
/// Falls back to a placeholder connection string so migrations can be generated offline.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Schema generation is offline: never discover a production connection from local secrets.
        const string connectionString = "Server=localhost;Database=Website_Application_MarksBaseballCardsDb;Integrated Security=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
