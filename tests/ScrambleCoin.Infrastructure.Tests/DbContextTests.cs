using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests;

/// <summary>
/// Verifies that <see cref="ScrambleCoinDbContext"/> can be instantiated and operated
/// against an EF Core InMemory provider — no real SQL Server required.
/// </summary>
public class DbContextTests
{
    private static DbContextOptions<ScrambleCoinDbContext> BuildInMemoryOptions(string dbName) =>
        new DbContextOptionsBuilder<ScrambleCoinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

    [Fact]
    public void ScrambleCoinDbContext_CanBeInstantiated_WithInMemoryOptions()
    {
        var options = BuildInMemoryOptions(nameof(ScrambleCoinDbContext_CanBeInstantiated_WithInMemoryOptions));

        using var context = new ScrambleCoinDbContext(options);

        Assert.NotNull(context);
    }

    [Fact]
    public void ScrambleCoinDbContext_EnsureCreated_ReturnsTrueForFreshDatabase()
    {
        var options = BuildInMemoryOptions(nameof(ScrambleCoinDbContext_EnsureCreated_ReturnsTrueForFreshDatabase));

        using var context = new ScrambleCoinDbContext(options);
        var created = context.Database.EnsureCreated();

        Assert.True(created);
    }

    [Fact]
    public async Task ScrambleCoinDbContext_SaveChangesAsync_ReturnsZero_WhenNoChanges()
    {
        var options = BuildInMemoryOptions(nameof(ScrambleCoinDbContext_SaveChangesAsync_ReturnsZero_WhenNoChanges));

        await using var context = new ScrambleCoinDbContext(options);
        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(0, affectedRows);
    }

    [Fact]
    public void ScrambleCoinDbContext_DatabaseProvider_IsInMemory()
    {
        var options = BuildInMemoryOptions(nameof(ScrambleCoinDbContext_DatabaseProvider_IsInMemory));

        using var context = new ScrambleCoinDbContext(options);

        Assert.True(context.Database.IsInMemory());
    }
}
