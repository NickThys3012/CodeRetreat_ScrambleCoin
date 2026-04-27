using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using ScrambleCoin.Web.Pages;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// bUnit component tests for <c>Changelog.razor</c> — Issue #18.
///
/// Each test creates its own <c>BunitContext</c> using <c>await using</c> to guarantee
/// that <c>DisposeAsync()</c> is called (required because MudBlazor's
/// <c>KeyInterceptorService</c> only implements <c>IAsyncDisposable</c>).
///
/// Test matrix
/// ───────────
///   T1  File absent                     → "No releases yet" alert shown
///   T2  File present, empty array []    → "No releases yet" alert shown
///   T3  File present, one valid entry   → version text rendered
///   T4  File present, one valid entry   → PR title rendered
///   T5  File present, one valid entry   → release date rendered
///   T6  File present, entry with notes  → note bullet text rendered
///   T7  File present, valid entry       → "No releases yet" NOT shown
///   T8  Malformed JSON                  → "No releases yet" shown (no crash)
///   T9  Malformed JSON                  → loading spinner NOT shown after init
///   T10 Multiple entries                → all version chips rendered
/// </summary>
public sealed class ChangelogComponentTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Creates an isolated temporary directory for each test.</summary>
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Builds a <see cref="BunitContext"/> with MudBlazor and logging services registered
    /// and <paramref name="webRootPath"/> wired as the <see cref="IWebHostEnvironment"/>.
    /// </summary>
    private static BunitContext CreateContext(string webRootPath)
    {
        var ctx = new BunitContext();
        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        ctx.Services.AddSingleton<IWebHostEnvironment>(
            new FakeWebHostEnvironment { WebRootPath = webRootPath });
        return ctx;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T1 — File absent → "No releases yet"
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>When changelog.json does not exist the page shows the empty-state alert.</summary>
    [Fact]
    public async Task Changelog_WhenFileAbsent_ShowsNoReleasesYet()
    {
        var dir = CreateTempDir();
        try
        {
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("No releases yet", cut.Markup, StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T2 — Empty JSON array → "No releases yet"
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>When changelog.json contains an empty array the page shows the empty-state alert.</summary>
    [Fact]
    public async Task Changelog_WhenFileContainsEmptyArray_ShowsNoReleasesYet()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), "[]");
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("No releases yet", cut.Markup, StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T3 — Valid entry → version text rendered
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>When a valid entry exists the version string is present in the rendered markup.</summary>
    [Fact]
    public async Task Changelog_WhenEntryExists_RendersVersion()
    {
        const string json = """
            [
              {
                "version": "v1.2.3",
                "date":    "2024-01-15",
                "title":   "My Feature Release",
                "notes":   ["Added something cool"]
              }
            ]
            """;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), json);
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("v1.2.3", cut.Markup, StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T4 — Valid entry → PR title rendered
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>The entry's PR title is visible in the rendered markup.</summary>
    [Fact]
    public async Task Changelog_WhenEntryExists_RendersPrTitle()
    {
        const string json = """
            [
              {
                "version": "v1.0.1",
                "date":    "2024-01-15",
                "title":   "First Release Ever",
                "notes":   []
              }
            ]
            """;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), json);
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("First Release Ever", cut.Markup, StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T5 — Valid entry → release date rendered
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>The entry's date is visible in the rendered markup.</summary>
    [Fact]
    public async Task Changelog_WhenEntryExists_RendersDate()
    {
        const string json = """
            [
              {
                "version": "v1.0.1",
                "date":    "2024-03-07",
                "title":   "Date Test",
                "notes":   []
              }
            ]
            """;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), json);
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("2024-03-07", cut.Markup, StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T6 — Entry with notes → note bullet text rendered
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Release note bullets are rendered inside the entry card.</summary>
    [Fact]
    public async Task Changelog_WhenEntryHasNotes_RendersNoteText()
    {
        const string json = """
            [
              {
                "version": "v1.0.1",
                "date":    "2024-02-01",
                "title":   "Patch",
                "notes":   ["Fixed login bug", "Improved performance"]
              }
            ]
            """;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), json);
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("Fixed login bug", cut.Markup, StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T7 — Valid entry → empty-state alert NOT shown
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>When entries exist the "No releases yet" alert must not appear.</summary>
    [Fact]
    public async Task Changelog_WhenEntriesExist_DoesNotShowEmptyStateAlert()
    {
        const string json = """
            [
              {
                "version": "v2.0.0",
                "date":    "2024-06-01",
                "title":   "Major Release",
                "notes":   ["Breaking change A"]
              }
            ]
            """;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), json);
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            // Wait for the component to finish loading (version must appear)
            cut.WaitForAssertion(
                () => Assert.Contains("v2.0.0", cut.Markup, StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));
            // Then assert the negative
            Assert.DoesNotContain("No releases yet", cut.Markup, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T8 — Malformed JSON → "No releases yet" (graceful degradation)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Malformed JSON is caught and the page falls back to the empty state without crashing.</summary>
    [Fact]
    public async Task Changelog_WhenJsonIsMalformed_ShowsNoReleasesYet()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), "not { valid } json !!!");
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            cut.WaitForAssertion(
                () => Assert.Contains("No releases yet", cut.Markup, StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(2));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T9 — Malformed JSON → loading spinner not visible after init
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>After OnInitializedAsync completes the loading spinner must be hidden, even on error.</summary>
    [Fact]
    public async Task Changelog_WhenJsonIsMalformed_LoadingSpinnerIsNotVisible()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), "{\"broken\": true}");
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            // Wait for loading to finish: the "No releases yet" alert must appear
            cut.WaitForAssertion(
                () => Assert.Contains("No releases yet", cut.Markup, StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(2));
            // The spinner is rendered only while _loading=true; after init it must be gone.
            // MudProgressCircular renders a <div role="progressbar"> element.
            Assert.DoesNotContain("progressbar", cut.Markup, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T10 — Multiple entries → all version chips rendered
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Every entry in the JSON array produces its own version chip in the markup.</summary>
    [Fact]
    public async Task Changelog_WhenMultipleEntriesExist_RendersAllVersions()
    {
        const string json = """
            [
              {
                "version": "v1.2.0",
                "date":    "2024-05-01",
                "title":   "Minor Release",
                "notes":   []
              },
              {
                "version": "v1.1.0",
                "date":    "2024-04-01",
                "title":   "Another Minor",
                "notes":   []
              },
              {
                "version": "v1.0.1",
                "date":    "2024-03-01",
                "title":   "First Patch",
                "notes":   []
              }
            ]
            """;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.json"), json);
            await using var ctx = CreateContext(dir);
            var cut = ctx.Render<Changelog>();
            // Wait for the first entry to render, then check all three
            cut.WaitForAssertion(
                () => Assert.Contains("v1.2.0", cut.Markup, StringComparison.Ordinal),
                TimeSpan.FromSeconds(2));
            Assert.Contains("v1.1.0", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("v1.0.1", cut.Markup, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Private test double ───────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="IWebHostEnvironment"/> stub.
    /// Only <see cref="WebRootPath"/> matters for <c>Changelog.razor</c>;
    /// everything else is set to benign defaults.
    /// </summary>
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ScrambleCoin.Web";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
