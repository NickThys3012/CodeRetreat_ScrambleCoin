using Microsoft.EntityFrameworkCore;

namespace ScrambleCoin.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext stub for ScrambleCoin.
/// Migrations and entity configurations will be added in Issue #2.
/// </summary>
public class ScrambleCoinDbContext : DbContext
{
    public ScrambleCoinDbContext(DbContextOptions<ScrambleCoinDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations will be registered here as they are created.
    }
}
