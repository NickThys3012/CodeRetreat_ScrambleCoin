using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// Provides a <see cref="ScrambleCoinDbContext"/> at design time so that
/// <c>dotnet ef migrations add</c> and <c>dotnet ef database update</c> can run
/// from the Infrastructure project using the connection string from appsettings.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ScrambleCoinDbContext>
{
    public ScrambleCoinDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "ScrambleCoin.Web"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ScrambleCoin;Trusted_Connection=True;";

        var options = new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly("ScrambleCoin.Infrastructure"))
            .Options;

        return new ScrambleCoinDbContext(options);
    }
}
