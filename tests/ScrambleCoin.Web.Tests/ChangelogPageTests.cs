using System.Text.Json;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Static source-file assertions for the Changelog page introduced in Issue #18.
/// Verifies structural and UI requirements by reading the .razor source directly —
/// no Blazor runtime required.
///
/// Acceptance criteria covered:
///   AC1  — @page "/changelog" route directive is present
///   AC2  — IWebHostEnvironment is injected to locate wwwroot
///   AC3  — ILogger&lt;Changelog&gt; is injected for error-path logging
///   AC4  — MudProgressCircular loading spinner is defined
///   AC5  — "No releases yet" message is defined for the empty state
///   AC6  — MudTimeline is used to display release history
///   AC7  — ChangelogEntry record maps version, date, title and notes
///   AC8  — Malformed JSON is guarded by a try/catch block with Logger.LogWarning
///   AC9  — changelog.json seed file exists in wwwroot, is valid JSON and an array
///   AC10 — changelog.json is NOT listed in .gitignore
/// </summary>
public class ChangelogPageTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string WebProjectDir = Path.Combine(RepoRoot, "src", "ScrambleCoin.Web");

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScrambleCoin.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate ScrambleCoin.sln by walking up from the test output directory.");
    }

    private static string ReadChangelogRazor()
    {
        var path = Path.Combine(WebProjectDir, "Pages", "Changelog.razor");
        Assert.True(File.Exists(path), $"Changelog.razor not found: {path}");
        return File.ReadAllText(path);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC1 — @page "/changelog" route
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor is routed to /changelog.</summary>
    [Fact]
    public void ChangelogRazor_HasPageRouteSlashChangelog()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains(@"@page ""/changelog""", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC2 — IWebHostEnvironment injection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor injects IWebHostEnvironment to resolve wwwroot.</summary>
    [Fact]
    public void ChangelogRazor_InjectsIWebHostEnvironment()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("IWebHostEnvironment", razor, StringComparison.Ordinal);
    }

    /// <summary>Changelog.razor accesses WebRootPath from the injected environment.</summary>
    [Fact]
    public void ChangelogRazor_UsesWebRootPath()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("WebRootPath", razor, StringComparison.Ordinal);
    }

    /// <summary>Changelog.razor builds the file path using changelog.json.</summary>
    [Fact]
    public void ChangelogRazor_ReadsChangelogJson()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("changelog.json", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC3 — ILogger injection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor injects ILogger for warning/error logging.</summary>
    [Fact]
    public void ChangelogRazor_InjectsILogger()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("ILogger", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC4 — Loading spinner
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor defines a _loading flag to track async init.</summary>
    [Fact]
    public void ChangelogRazor_HasLoadingFlag()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("_loading", razor, StringComparison.Ordinal);
    }

    /// <summary>Changelog.razor shows a MudProgressCircular spinner while loading.</summary>
    [Fact]
    public void ChangelogRazor_ShowsMudProgressCircularWhileLoading()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("MudProgressCircular", razor, StringComparison.Ordinal);
    }

    /// <summary>The spinner is rendered inside an @if (_loading) block.</summary>
    [Fact]
    public void ChangelogRazor_LoadingSpinner_IsConditionalOnLoadingFlag()
    {
        var razor = ReadChangelogRazor();

        var loadingIfIndex = razor.IndexOf("@if (_loading)", StringComparison.Ordinal);
        var spinnerIndex   = razor.IndexOf("MudProgressCircular", StringComparison.Ordinal);

        Assert.True(loadingIfIndex >= 0, "@if (_loading) not found in Changelog.razor.");
        Assert.True(spinnerIndex   >= 0, "MudProgressCircular not found in Changelog.razor.");
        Assert.True(
            spinnerIndex > loadingIfIndex,
            "MudProgressCircular must appear inside the @if (_loading) block.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC5 — "No releases yet" empty-state message
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor contains the "No releases yet" empty-state text.</summary>
    [Fact]
    public void ChangelogRazor_ContainsNoReleasesYetText()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("No releases yet", razor, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The empty-state message is rendered inside a MudAlert component.</summary>
    [Fact]
    public void ChangelogRazor_EmptyState_UsedMudAlert()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("MudAlert", razor, StringComparison.Ordinal);
    }

    /// <summary>The empty state is rendered when _entries are null or empty.</summary>
    [Fact]
    public void ChangelogRazor_EmptyState_ConditionalOnEmptyEntries()
    {
        var razor = ReadChangelogRazor();
        // The razor should test _entries for null / empty before showing the alert
        Assert.Contains("_entries", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC6 — MudTimeline for release history
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor uses MudTimeline to present the release list.</summary>
    [Fact]
    public void ChangelogRazor_ContainsMudTimeline()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("MudTimeline", razor, StringComparison.Ordinal);
    }

    /// <summary>Each entry is wrapped in a MudTimelineItem.</summary>
    [Fact]
    public void ChangelogRazor_ContainsMudTimelineItem()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("MudTimelineItem", razor, StringComparison.Ordinal);
    }

    /// <summary>Changelog.razor iterates entries with @foreach.</summary>
    [Fact]
    public void ChangelogRazor_IteratesEntriesWithForeach()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("@foreach", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC7 — ChangelogEntry record shape
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>A ChangelogEntry record type is defined in the component.</summary>
    [Fact]
    public void ChangelogRazor_DefinesChangelogEntryRecord()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("ChangelogEntry", razor, StringComparison.Ordinal);
    }

    /// <summary>The ChangelogEntry exposes a Version property.</summary>
    [Fact]
    public void ChangelogRazor_ChangelogEntry_HasVersionProperty()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("Version", razor, StringComparison.Ordinal);
    }

    /// <summary>The ChangelogEntry exposes a Date property.</summary>
    [Fact]
    public void ChangelogRazor_ChangelogEntry_HasDateProperty()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("Date", razor, StringComparison.Ordinal);
    }

    /// <summary>The ChangelogEntry exposes a Title property.</summary>
    [Fact]
    public void ChangelogRazor_ChangelogEntry_HasTitleProperty()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("Title", razor, StringComparison.Ordinal);
    }

    /// <summary>The ChangelogEntry exposes a Notes property (list of strings).</summary>
    [Fact]
    public void ChangelogRazor_ChangelogEntry_HasNotesProperty()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("Notes", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC8 — Error handling (malformed JSON / missing file)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Changelog.razor wraps the file read in a try/catch block.</summary>
    [Fact]
    public void ChangelogRazor_HasTryCatchAroundFileRead()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("catch", razor, StringComparison.Ordinal);
    }

    /// <summary>In error, Logger.LogWarning is called.</summary>
    [Fact]
    public void ChangelogRazor_LogsWarningOnException()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("LogWarning", razor, StringComparison.Ordinal);
    }

    /// <summary>A final block ensures _loading is always set to false.</summary>
    [Fact]
    public void ChangelogRazor_HasFinallyBlockToResetLoading()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("finally", razor, StringComparison.Ordinal);
    }

    /// <summary>File existence is checked with File.Exists before reading.</summary>
    [Fact]
    public void ChangelogRazor_ChecksFileExistsBeforeReading()
    {
        var razor = ReadChangelogRazor();
        Assert.Contains("File.Exists", razor, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC9 — changelog.json seed file
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>changelog.json exists in wwwroot at the path the workflow writes to.</summary>
    [Fact]
    public void ChangelogJson_ExistsInWwwroot()
    {
        var path = Path.Combine(WebProjectDir, "wwwroot", "changelog.json");
        Assert.True(File.Exists(path), $"changelog.json not found: {path}");
    }

    /// <summary>changelog.json is valid JSON (no parse exception).</summary>
    [Fact]
    public void ChangelogJson_IsValidJson()
    {
        var path = Path.Combine(WebProjectDir, "wwwroot", "changelog.json");
        var json = File.ReadAllText(path);

        var exception = Record.Exception(() => JsonDocument.Parse(json));
        Assert.Null(exception);
    }

    /// <summary>changelog.json root element is a JSON array.</summary>
    [Fact]
    public void ChangelogJson_RootIsArray()
    {
        var path = Path.Combine(WebProjectDir, "wwwroot", "changelog.json");
        var json = File.ReadAllText(path);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC10 — changelog.json NOT gitignored
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>changelog.json is not listed in .gitignore so it survives dotnet publish.</summary>
    [Fact]
    public void GitIgnore_DoesNotExcludeChangelogJson()
    {
        var gitignorePath = Path.Combine(RepoRoot, ".gitignore");
        Assert.True(File.Exists(gitignorePath), ".gitignore not found at repo root.");

        var content = File.ReadAllText(gitignorePath);

        // The file must not have a rule that would exclude changelog.json.
        // A direct match on "changelog.json" is the clearest violation.
        Assert.DoesNotContain(
            "changelog.json",
            content,
            StringComparison.OrdinalIgnoreCase);
    }
}
