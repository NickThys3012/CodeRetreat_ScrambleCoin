using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Infrastructure.Tests;

/// <summary>
/// Verifies that the InitialCreate EF Core migration and related scaffolding exist
/// and are correctly structured (acceptance criteria 4 &amp; 5 of Issue #2).
/// All assertions use reflection — no live database required.
/// </summary>
public class MigrationTests
{
    private static readonly Assembly InfraAssembly =
        typeof(ScrambleCoinDbContext).Assembly;

    // ── Migration type exists ────────────────────────────────────────────────
    [Fact]
    public void InitialCreate_MigrationType_ExistsInMigrationsNamespace()
    {
        var migrationType = InfraAssembly.GetType(
            "ScrambleCoin.Infrastructure.Migrations.InitialCreate",
            throwOnError: false);

        Assert.NotNull(migrationType);
    }

    // ── Migration snapshot type exists ───────────────────────────────────────
    [Fact]
    public void ScrambleCoinDbContextModelSnapshot_ExistsInMigrationsNamespace()
    {
        var snapshotType = InfraAssembly.GetType(
            "ScrambleCoin.Infrastructure.Migrations.ScrambleCoinDbContextModelSnapshot",
            throwOnError: false);

        Assert.NotNull(snapshotType);
    }

    // ── Migration has an Up method ───────────────────────────────────────────
    [Fact]
    public void InitialCreate_HasUpMethod()
    {
        var migrationType = InfraAssembly.GetType(
            "ScrambleCoin.Infrastructure.Migrations.InitialCreate",
            throwOnError: true)!;

        var upMethod = migrationType.GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(upMethod);
    }

    // ── Migration has a Down method ──────────────────────────────────────────
    [Fact]
    public void InitialCreate_HasDownMethod()
    {
        var migrationType = InfraAssembly.GetType(
            "ScrambleCoin.Infrastructure.Migrations.InitialCreate",
            throwOnError: true)!;

        var downMethod = migrationType.GetMethod(
            "Down",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(downMethod);
    }

    // ── DbContext has the expected constructor ───────────────────────────────
    [Fact]
    public void ScrambleCoinDbContext_HasConstructor_AcceptingDbContextOptions()
    {
        var contextType = typeof(ScrambleCoinDbContext);

        var constructor = contextType.GetConstructor(
            new[] { typeof(DbContextOptions<ScrambleCoinDbContext>) });

        Assert.NotNull(constructor);
    }

    // ── DbContext is in the correct namespace ────────────────────────────────
    [Fact]
    public void ScrambleCoinDbContext_IsInPersistenceNamespace()
    {
        Assert.Equal(
            "ScrambleCoin.Infrastructure.Persistence",
            typeof(ScrambleCoinDbContext).Namespace);
    }

    // ── Migration derives from Microsoft.EntityFrameworkCore.Migrations.Migration ──
    [Fact]
    public void InitialCreate_DerivesFromMigrationBaseClass()
    {
        var migrationType = InfraAssembly.GetType(
            "ScrambleCoin.Infrastructure.Migrations.InitialCreate",
            throwOnError: true)!;

        Assert.True(
            typeof(Microsoft.EntityFrameworkCore.Migrations.Migration).IsAssignableFrom(migrationType),
            "InitialCreate should derive from Microsoft.EntityFrameworkCore.Migrations.Migration");
    }
}
