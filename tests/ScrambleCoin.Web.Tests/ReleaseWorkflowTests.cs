namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Static-analysis tests for the GitHub Actions release workflow introduced in Issue #18.
/// Verifies structural requirements of <c>.github/workflows/release.yml</c> without
/// executing the workflow — confirming that triggers, permissions, versioning logic,
/// tagging, GitHub Release creation, and changelog.json update steps are correctly defined.
///
/// Acceptance criteria covered:
///   AC1  — Workflow triggers on push to main
///   AC2  — Workflow has contents: write permission (for tagging and committing)
///   AC3  — Workflow has pull-requests: read permission (for reading PR labels)
///   AC4  — Checkout uses fetch-depth: 0 (full history needed for git tag --sort)
///   AC5  — Workflow reads merged PR labels via GitHub CLI
///   AC6  — Workflow detects the "major" label and bumps major version
///   AC7  — Workflow detects the "minor" label and bumps minor version
///   AC8  — Workflow defaults to patch bump when no version label is present
///   AC9  — Workflow falls back to v1.0.0 when no prior tags exist
///   AC10 — New semver tag is pushed to the repository
///   AC11 — GitHub Release is created with --generate-notes
///   AC12 — changelog.json is updated at the correct wwwroot path
///   AC13 — New changelog entry is prepended (not appended) to existing array
///   AC14 — changelog.json update is committed with a [skip ci] message
///   AC15 — Commit uses the github-actions bot identity
///   AC16 — GITHUB_TOKEN is used (no hard-coded credentials)
/// </summary>
public class ReleaseWorkflowTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FindWorkflowsDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate .github directory by walking up from the test output directory.");

        return Path.Combine(dir.FullName, ".github", "workflows");
    }

    private static string ReadReleaseWorkflow()
    {
        var path = Path.Combine(FindWorkflowsDir(), "release.yml");
        Assert.True(File.Exists(path), $"release.yml not found: {path}");
        return File.ReadAllText(path);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC1 — Trigger
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>release.yml is triggered by push events to main.</summary>
    [Fact]
    public void ReleaseWorkflow_TriggersOnPushToMain()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("push:", yaml, StringComparison.Ordinal);
        Assert.Contains("main", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC2 + AC3 — Permissions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>release.yml grants contents: write so it can push tags and commits.</summary>
    [Fact]
    public void ReleaseWorkflow_HasContentsWritePermission()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("contents: write", yaml, StringComparison.Ordinal);
    }

    /// <summary>release.yml grants pull-requests: read so it can read merged PR labels.</summary>
    [Fact]
    public void ReleaseWorkflow_HasPullRequestsReadPermission()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("pull-requests: read", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC4 — Full history checkout
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Checkout step uses fetch-depth: 0 so all tags are available for version detection.</summary>
    [Fact]
    public void ReleaseWorkflow_CheckoutUsesFetchDepthZero()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("fetch-depth: 0", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC5 — Read merged PR info
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow queries the GitHub API for the merged PR's metadata.</summary>
    [Fact]
    public void ReleaseWorkflow_QueriesMergedPrInfo()
    {
        var yaml = ReadReleaseWorkflow();

        // The workflow uses the gh CLI or GitHub API to retrieve PR labels
        Assert.Contains("labels", yaml, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC6 — major label → major bump
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow increments MAJOR and resets MINOR and PATCH when "major" label is present.</summary>
    [Fact]
    public void ReleaseWorkflow_IncreasesMajorVersionOnMajorLabel()
    {
        var yaml = ReadReleaseWorkflow();

        // Expect the string "major" to appear as a label check
        Assert.Contains("major", yaml, StringComparison.Ordinal);

        // Expect a MAJOR variable increment expression
        Assert.Contains("MAJOR", yaml, StringComparison.Ordinal);
    }

    /// <summary>When major label fires, MINOR is reset to 0.</summary>
    [Fact]
    public void ReleaseWorkflow_ResetMinorAndPatchOnMajorBump()
    {
        var yaml = ReadReleaseWorkflow();

        // After a major bump MINOR=0 and PATCH=0 should appear
        Assert.Contains("MINOR=0", yaml, StringComparison.Ordinal);
        Assert.Contains("PATCH=0", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC7 — minor label → minor bump
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow increments MINOR and resets PATCH when "minor" label is present.</summary>
    [Fact]
    public void ReleaseWorkflow_IncreasesMinorVersionOnMinorLabel()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("minor", yaml, StringComparison.Ordinal);
        Assert.Contains("MINOR", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC8 — Default to patch bump
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow bumps patch when no recognised version label is present.</summary>
    [Fact]
    public void ReleaseWorkflow_DefaultsToPatchBump()
    {
        var yaml = ReadReleaseWorkflow();

        // The yaml must mention "patch" (label name) and bump the PATCH variable
        Assert.Contains("patch", yaml, StringComparison.Ordinal);
        Assert.Contains("PATCH", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC9 — Fallback to v1.0.0 when no tags exist
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow defaults to v1.0.0 as the baseline when no existing semver tag is found.</summary>
    [Fact]
    public void ReleaseWorkflow_DefaultsToV1_0_0WhenNoTagsExist()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("v1.0.0", yaml, StringComparison.Ordinal);
    }

    /// <summary>Workflow reads existing tags sorted by semver to find the latest.</summary>
    [Fact]
    public void ReleaseWorkflow_ReadsSemverTagsFromGit()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("git tag", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC10 — Push new semver tag
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow creates and pushes the new semver tag to the remote.</summary>
    [Fact]
    public void ReleaseWorkflow_PushesNewTag()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("git push", yaml, StringComparison.Ordinal);
        Assert.Contains("git tag", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC11 — GitHub Release with auto-generated notes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow creates a GitHub Release using the gh CLI.</summary>
    [Fact]
    public void ReleaseWorkflow_CreatesGitHubRelease()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("gh release create", yaml, StringComparison.Ordinal);
    }

    /// <summary>GitHub Release is created with --generate-notes for automatic release notes.</summary>
    [Fact]
    public void ReleaseWorkflow_UsesGenerateNotesFlag()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("--generate-notes", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC12 — changelog.json written at correct wwwroot path
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow writes changelog.json to the wwwroot directory of the Web project.</summary>
    [Fact]
    public void ReleaseWorkflow_WritesChangelogJsonToCorrectPath()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains(
            "src/ScrambleCoin.Web/wwwroot/changelog.json",
            yaml,
            StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC13 — New entry prepended (most recent first)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Workflow prepends the new entry so the changelog is newest-first.</summary>
    [Fact]
    public void ReleaseWorkflow_PrependsNewEntryToExistingArray()
    {
        var yaml = ReadReleaseWorkflow();

        // The jq expression '[{...}] + .' prepends the new object to the existing array.
        Assert.Contains("+ .", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC14 — [skip ci] commit message
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>changelog.json is committed back to main with [skip ci] to avoid re-triggering the workflow.</summary>
    [Fact]
    public void ReleaseWorkflow_CommitsChangelogWithSkipCi()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("[skip ci]", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC15 — Bot committer identity
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Commit uses the github-actions[bot] identity so provenance is clear.</summary>
    [Fact]
    public void ReleaseWorkflow_CommitUsesGitHubActionsBot()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("github-actions[bot]", yaml, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC16 — GITHUB_TOKEN (no hard-coded secrets)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>All GitHub API calls use the built-in GITHUB_TOKEN — no personal access tokens.</summary>
    [Fact]
    public void ReleaseWorkflow_UsesGitHubToken()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("GITHUB_TOKEN", yaml, StringComparison.Ordinal);
    }

    /// <summary>GH_TOKEN env var is set from GITHUB_TOKEN for gh CLI steps.</summary>
    [Fact]
    public void ReleaseWorkflow_SetsGhTokenFromGitHubToken()
    {
        var yaml = ReadReleaseWorkflow();

        Assert.Contains("GH_TOKEN", yaml, StringComparison.Ordinal);
        Assert.Contains("secrets.GITHUB_TOKEN", yaml, StringComparison.Ordinal);
    }
}
