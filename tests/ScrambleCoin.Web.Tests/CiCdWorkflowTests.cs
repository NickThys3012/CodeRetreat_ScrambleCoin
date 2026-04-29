namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Static-analysis tests for the GitHub Actions workflow YAML files introduced in Issue #4.
/// These tests verify structural requirements of ci.yml and release-and-deploy.yml without executing the
/// workflows themselves — ensuring that required job names, SDK versions, steps, secrets,
/// and deployment targets are present and correctly configured.
/// </summary>
public class CiCdWorkflowTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from the test-output directory until a <c>.GitHub</c> folder is found,
    /// then returns the path to <c>.GitHub/workflows</c>.
    /// </summary>
    private static string FindWorkflowsDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate the .github directory by walking up from the test output directory.");

        return Path.Combine(dir.FullName, ".github", "workflows");
    }

    private static string ReadWorkflow(string fileName)
    {
        var path = Path.Combine(FindWorkflowsDir(), fileName);
        Assert.True(File.Exists(path), $"Workflow file not found: {path}");
        return File.ReadAllText(path);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ci.yml — Pull Request checks
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Test 1 — The CI workflow is triggered by pull requests targeting main.</summary>
    [Fact]
    public void CiWorkflow_TriggersOnPullRequestToMain()
    {
        var yaml = ReadWorkflow("ci.yml");

        Assert.Contains("pull_request", yaml, StringComparison.Ordinal);
        Assert.Contains("branches:", yaml, StringComparison.Ordinal);
        Assert.Contains("main", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 2 — The CI workflow uses .NET 9 SDK.</summary>
    [Fact]
    public void CiWorkflow_UsesNET9Sdk()
    {
        var yaml = ReadWorkflow("ci.yml");

        Assert.Contains("dotnet-version: '9.0.x'", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 3 — The CI workflow runs <c>dotnet test</c>.</summary>
    [Fact]
    public void CiWorkflow_RunsDotnetTest()
    {
        var yaml = ReadWorkflow("ci.yml");

        Assert.Contains("dotnet test", yaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 4 — The CI job is named <c>build-and-test</c>, matching the required status-check
    /// name that must be configured in the branch protection rule for <c>main</c>.
    /// </summary>
    [Fact]
    public void CiWorkflow_JobNameIsBuildAndTest()
    {
        var yaml = ReadWorkflow("ci.yml");

        Assert.Contains("build-and-test", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 5 — The CI workflow uploads test results as a workflow artefact.</summary>
    [Fact]
    public void CiWorkflow_UploadsTestResultsArtifact()
    {
        var yaml = ReadWorkflow("ci.yml");

        Assert.Contains("upload-artifact", yaml, StringComparison.Ordinal);
        Assert.Contains("TestResults", yaml, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Test 6 — The CI workflow builds and tests in Release configuration.</summary>
    [Fact]
    public void CiWorkflow_BuildsInReleaseConfiguration()
    {
        var yaml = ReadWorkflow("ci.yml");

        Assert.Contains("--configuration Release", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // release-and-deploy.yml — Deploy on merge to main
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Test 7 — The CD workflow is triggered by pushes to the main.</summary>
    [Fact]
    public void CdWorkflow_TriggersOnPushToMain()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("push:", yaml, StringComparison.Ordinal);
        Assert.Contains("branches:", yaml, StringComparison.Ordinal);
        Assert.Contains("main", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 8 — The CD workflow uses .NET 9 SDK.</summary>
    [Fact]
    public void CdWorkflow_UsesNET9Sdk()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("dotnet-version: '9.0.x'", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 9 — The CD workflow runs <c>dotnet test</c> before deploying.</summary>
    [Fact]
    public void CdWorkflow_RunsDotnetTest()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("dotnet test", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 10 — The CD workflow publishes the ScrambleCoin.Web project.</summary>
    [Fact]
    public void CdWorkflow_PublishesWebApp()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("dotnet publish", yaml, StringComparison.Ordinal);
        Assert.Contains("ScrambleCoin.Web", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 11 — The CD workflow installs the EF Core global CLI tool.</summary>
    [Fact]
    public void CdWorkflow_InstallsEfCoreTool()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("dotnet tool install", yaml, StringComparison.Ordinal);
        Assert.Contains("dotnet-ef", yaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 12 — The EF Core CLI tool is pinned to a version-9 release
    /// (<c>--version 9.*</c>) so minor/patch updates are accepted automatically
    /// while a future major upgrade remains a deliberate choice.
    /// </summary>
    [Fact]
    public void CdWorkflow_EfToolPinnedToVersion9()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("--version 9.", yaml, StringComparison.Ordinal);
    }

    /// <summary>Test 13 — The CD workflow runs EF Core database migrations.</summary>
    [Fact]
    public void CdWorkflow_RunsMigrations()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("dotnet ef database update", yaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 14 — The EF Core migration step uses Release configuration, ensuring it
    /// resolves the same startup project build output as the publication step.
    /// </summary>
    [Fact]
    public void CdWorkflow_MigrationsUseReleaseConfiguration()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        // Verify both tokens appear in the file — the migration step carries
        // --configuration Release alongside the dotnet ef database update command.
        var migrationIndex = yaml.IndexOf("dotnet ef database update", StringComparison.Ordinal);
        Assert.True(migrationIndex >= 0, "'dotnet ef database update' not found in release-and-deploy.yml.");

        // Look for '--configuration Release' anywhere after the migration command
        // (multi-line YAML scalar: the flag appears on a later line of the same step).
        var releaseIndex = yaml.IndexOf("--configuration Release", migrationIndex, StringComparison.Ordinal);
        Assert.True(
            releaseIndex >= 0,
            "'--configuration Release' was not found after 'dotnet ef database update' in release-and-deploy.yml.");
    }

    /// <summary>
    /// Test 15 — The CD workflow deploys to Azure App Service using the
    /// <c>azure/webapps-deploy</c> action targeting the <c>app-scramblecoin</c> app.
    /// </summary>
    [Fact]
    public void CdWorkflow_DeploysToAzureWebApp()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("azure/webapps-deploy", yaml, StringComparison.Ordinal);
        Assert.Contains("app-scramblecoin", yaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 16 — The CD workflow reads the Azure App Service publish profile from the
    /// <c>AZURE_WEBAPP_PUBLISH_PROFILE</c> repository secret, ensuring deployment
    /// credentials are never hard-coded.
    /// </summary>
    [Fact]
    public void CdWorkflow_UsesPublishProfileSecret()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("AZURE_WEBAPP_PUBLISH_PROFILE", yaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 17 — The CD workflow injects the SQL Server connection string from the
    /// <c>AZURE_SQL_CONNECTION_STRING</c> repository secret so that EF Core migrations
    /// target the production database without exposing credentials in the workflow file.
    /// </summary>
    [Fact]
    public void CdWorkflow_UsesSqlConnectionStringSecret()
    {
        var yaml = ReadWorkflow("release-and-deploy.yml");

        Assert.Contains("AZURE_SQL_CONNECTION_STRING", yaml, StringComparison.Ordinal);
    }
}
