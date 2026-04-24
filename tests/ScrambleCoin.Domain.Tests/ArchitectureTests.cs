using System.Xml.Linq;

namespace ScrambleCoin.Domain.Tests;

/// <summary>
/// Architecture guard tests — verify Clean Architecture dependency rules
/// and that the solution file contains the expected 9 projects.
/// These tests are purely reflection/file-based; they require no infrastructure.
/// </summary>
public class ArchitectureTests
{
    // Walk upwards from the output directory until we find ScrambleCoin.sln
    private static readonly string RepoRoot = LocateRepoRoot();

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScrambleCoin.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate ScrambleCoin.sln by walking up from the test output directory.");
    }

    private static XDocument LoadCsproj(string relativeProjectPath)
    {
        var fullPath = Path.Combine(RepoRoot, relativeProjectPath);
        return XDocument.Load(fullPath);
    }

    private static IReadOnlyList<string> GetProjectReferences(XDocument csproj) =>
        csproj.Descendants("ProjectReference")
              .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
              .ToList();

    // ── Acceptance criterion 1 ──────────────────────────────────────────────
    [Fact]
    public void Solution_ContainsNineProjects()
    {
        var sln = Path.Combine(RepoRoot, "ScrambleCoin.sln");
        var content = File.ReadAllText(sln);

        // Count distinct .csproj references in the solution file
        var csprojCount = content
            .Split('\n')
            .Count(line => line.Contains(".csproj", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(9, csprojCount);
    }

    // ── Clean Architecture rule: Domain has zero project dependencies ───────
    [Fact]
    public void Domain_HasNoProjectReferences()
    {
        var doc = LoadCsproj(@"src/ScrambleCoin.Domain/ScrambleCoin.Domain.csproj");
        var refs = GetProjectReferences(doc);

        Assert.Empty(refs);
    }

    // ── Clean Architecture rule: Application references only Domain ─────────
    [Fact]
    public void Application_ReferencesOnlyDomain()
    {
        var doc = LoadCsproj(@"src/ScrambleCoin.Application/ScrambleCoin.Application.csproj");
        var refs = GetProjectReferences(doc);

        // Every reference must point at the Domain project
        Assert.All(refs, r => Assert.Contains("ScrambleCoin.Domain", r));
    }

    // ── Clean Architecture rule: Infrastructure does not reference Web ───────
    [Fact]
    public void Infrastructure_DoesNotReferenceWeb()
    {
        var doc = LoadCsproj(@"src/ScrambleCoin.Infrastructure/ScrambleCoin.Infrastructure.csproj");
        var refs = GetProjectReferences(doc);

        Assert.DoesNotContain(refs, r => r.Contains("ScrambleCoin.Web", StringComparison.OrdinalIgnoreCase));
    }

    // ── Clean Architecture rule: Domain.Tests only references Domain ─────────
    [Fact]
    public void DomainTests_ReferencesOnlyDomain()
    {
        var doc = LoadCsproj(@"tests/ScrambleCoin.Domain.Tests/ScrambleCoin.Domain.Tests.csproj");
        var refs = GetProjectReferences(doc);

        Assert.All(refs, r => Assert.Contains("ScrambleCoin.Domain", r));
    }

    // ── Solution structure: all expected project names present in .sln ───────
    [Theory]
    [InlineData("ScrambleCoin.Domain")]
    [InlineData("ScrambleCoin.Application")]
    [InlineData("ScrambleCoin.Infrastructure")]
    [InlineData("ScrambleCoin.Web")]
    [InlineData("ScrambleCoin.Domain.Tests")]
    [InlineData("ScrambleCoin.Application.Tests")]
    [InlineData("ScrambleCoin.Infrastructure.Tests")]
    [InlineData("ScrambleCoin.Web.Tests")]
    [InlineData("ScrambleCoin.E2E.Tests")]
    public void Solution_ContainsProject(string projectName)
    {
        var sln = Path.Combine(RepoRoot, "ScrambleCoin.sln");
        var content = File.ReadAllText(sln);

        Assert.Contains(projectName, content, StringComparison.OrdinalIgnoreCase);
    }
}
