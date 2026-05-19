using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// Provides a <see cref="ScrambleCoinDbContext"/> at design time so that
/// <c>dotnet ef migrations add</c> can run from the Infrastructure project
/// without needing the Web host or a live database connection.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ScrambleCoinDbContext>
{
    public ScrambleCoinDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=ScrambleCoin;Trusted_Connection=True;",
                sql => sql.MigrationsAssembly("ScrambleCoin.Infrastructure"))
            .Options;

        return new ScrambleCoinDbContext(options);
    }
}
