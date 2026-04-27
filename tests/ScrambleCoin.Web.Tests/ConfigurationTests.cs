using System.Text.Json;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Verifies configuration file conventions introduced in Issue #2:
/// - The tracked example settings file exists and is well-formed.
/// - The actual Development settings file is gitignored.
/// </summary>
public class ConfigurationTests
{
    // Walk upward from the test output directory to find the repo root
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string WebProjectDir =
        Path.Combine(RepoRoot, "src", "ScrambleCoin.Web");

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScrambleCoin.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate ScrambleCoin.sln by walking up from the test output directory.");
    }

    // ── Example settings file is tracked ────────────────────────────────────
    [Fact]
    public void AppsettingsDevelopmentExampleJson_ExistsInWebProjectDirectory()
    {
        var exampleFilePath = Path.Combine(WebProjectDir, "appsettings.Development.example.json");

        Assert.True(
            File.Exists(exampleFilePath),
            $"Expected file not found: {exampleFilePath}");
    }

    // ── Example settings file contains the DefaultConnection key ─────────────
    [Fact]
    public void AppsettingsDevelopmentExampleJson_ContainsConnectionStringsDefaultConnection()
    {
        var exampleFilePath = Path.Combine(WebProjectDir, "appsettings.Development.example.json");
        var json = File.ReadAllText(exampleFilePath);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(
            root.TryGetProperty("ConnectionStrings", out var connectionStrings),
            "appsettings.Development.example.json must contain a 'ConnectionStrings' section.");

        Assert.True(
            connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection),
            "ConnectionStrings section must contain a 'DefaultConnection' key.");

        var value = defaultConnection.GetString();
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            "ConnectionStrings:DefaultConnection must not be empty.");
    }

    // ── Actual Development settings is gitignored ────────────────────────────
    [Fact]
    public void GitIgnore_ContainsAppsettingsDevelopmentJson()
    {
        var gitignorePath = Path.Combine(RepoRoot, ".gitignore");

        Assert.True(File.Exists(gitignorePath), ".gitignore not found at repo root.");

        var content = File.ReadAllText(gitignorePath);

        Assert.Contains(
            "appsettings.Development.json",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── docker-compose.yml exposes SQL Server on port 1433 ──────────────────
    [Fact]
    public void DockerCompose_ExposesPort1433ForSqlServer()
    {
        var composePath = Path.Combine(RepoRoot, "docker-compose.yml");

        Assert.True(File.Exists(composePath), "docker-compose.yml not found at repo root.");

        var content = File.ReadAllText(composePath);
        Assert.Contains("1433:1433", content, StringComparison.Ordinal);
    }
}
