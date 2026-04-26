using System.Text.Json;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Verifies the Azure Bicep infrastructure files introduced in Issue #3,
/// without requiring a live Azure deployment.
/// </summary>
public class InfrastructureTemplateTests
{
    // Walk upward from the test output directory to find the repo root
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string BicepFilePath = Path.Combine(RepoRoot, "infra", "main.bicep");
    private static readonly string ParametersFilePath = Path.Combine(RepoRoot, "infra", "main.parameters.json");
    private static readonly string ReadmeFilePath = Path.Combine(RepoRoot, "README.md");

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScrambleCoin.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate ScrambleCoin.sln by walking up from the test output directory.");
    }

    // ── File existence ────────────────────────────────────────────────────────

    [Fact]
    public void BicepFile_ExistsAtExpectedPath()
    {
        Assert.True(
            File.Exists(BicepFilePath),
            $"Expected Bicep file not found: {BicepFilePath}");
    }

    [Fact]
    public void ParametersFile_ExistsAtExpectedPath()
    {
        Assert.True(
            File.Exists(ParametersFilePath),
            $"Expected parameters file not found: {ParametersFilePath}");
    }

    // ── Required Azure resource types ─────────────────────────────────────────

    [Fact]
    public void BicepFile_ContainsAppServicePlan()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("Microsoft.Web/serverfarms", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BicepFile_ContainsAppService()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("Microsoft.Web/sites", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BicepFile_ContainsApplicationInsights()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("Microsoft.Insights/components", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BicepFile_ContainsSqlServer()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("Microsoft.Sql/servers", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BicepFile_ContainsSqlDatabase()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("Microsoft.Sql/servers/databases", content, StringComparison.Ordinal);
    }

    // ── App Service wiring ────────────────────────────────────────────────────

    [Fact]
    public void BicepFile_WiresApplicationInsightsConnectionString()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("APPLICATIONINSIGHTS_CONNECTION_STRING", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BicepFile_UsesDotNet9Runtime()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("netFrameworkVersion", content, StringComparison.Ordinal);
        Assert.Contains("v9.0", content, StringComparison.Ordinal);
    }

    // ── Security: @secure() decorator & no hardcoded password ────────────────

    [Fact]
    public void BicepFile_UsesSecureDecoratorOnPassword()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.Contains("@secure()", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BicepFile_DoesNotContainHardcodedPassword()
    {
        var content = File.ReadAllText(BicepFilePath);

        Assert.DoesNotContain(
            "REPLACE_WITH_SECURE_PASSWORD",
            content,
            StringComparison.Ordinal);
    }

    // ── Parameters file ───────────────────────────────────────────────────────

    [Fact]
    public void ParametersFile_IsValidJson()
    {
        var json = File.ReadAllText(ParametersFilePath);

        // JsonDocument.Parse throws JsonException on invalid JSON
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void ParametersFile_ContainsSqlAdminPassword()
    {
        var json = File.ReadAllText(ParametersFilePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(
            root.TryGetProperty("parameters", out var parameters),
            "Parameters file must contain a top-level 'parameters' object.");

        Assert.True(
            parameters.TryGetProperty("sqlAdminPassword", out _),
            "parameters section must contain 'sqlAdminPassword'.");
    }

    [Fact]
    public void ParametersFile_SqlAdminPassword_IsPlaceholder()
    {
        var json = File.ReadAllText(ParametersFilePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var passwordValue = root
            .GetProperty("parameters")
            .GetProperty("sqlAdminPassword")
            .GetProperty("value")
            .GetString();

        Assert.Equal("REPLACE_WITH_SECURE_PASSWORD", passwordValue);
    }

    // ── README documentation ──────────────────────────────────────────────────

    [Fact]
    public void ReadmeFile_ContainsAzureDeploymentSection()
    {
        var content = File.ReadAllText(ReadmeFilePath);

        Assert.Contains("az deployment group create", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadmeFile_ContainsCostEstimate()
    {
        var content = File.ReadAllText(ReadmeFilePath);

        var hasCostInfo = content.Contains('€') || content.Contains("/month", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasCostInfo, "README must contain cost estimate information (look for '€' or '/month').");
    }
}
