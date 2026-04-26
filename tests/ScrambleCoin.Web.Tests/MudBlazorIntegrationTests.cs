namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Verifies the MudBlazor UI component library integration introduced in Issue #13
/// via static file-content assertions against the source files in the Web project.
///
/// Acceptance criteria tested:
///   AC1 — MudBlazor v9.4.0 NuGet package reference in ScrambleCoin.Web.csproj
///   AC2 — AddMudServices() called in Program.cs
///   AC3a — Roboto font link in Pages/_Host.cshtml
///   AC3b — MudBlazor.min.css link in Pages/_Host.cshtml
///   AC3c — MudBlazor.min.js script in Pages/_Host.cshtml
///   AC4 — @using MudBlazor in _Imports.razor
///   AC5a — MudThemeProvider in Shared/MainLayout.razor
///   AC5b — MudPopoverProvider in Shared/MainLayout.razor
///   AC5c — MudDialogProvider in Shared/MainLayout.razor
///   AC5d — MudSnackbarProvider in Shared/MainLayout.razor
///   AC6a — MudContainer used in Pages/Index.razor
///   AC6b — MudText used in Pages/Index.razor
///   AC6c — MudPaper used in Pages/Index.razor
///
/// DI registration tests (ISnackbar / IDialogService resolvable) live in
/// DiRegistrationTests.cs which already owns the shared WebApplicationFactory.
/// </summary>
public class MudBlazorIntegrationTests
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

    // ═══════════════════════════════════════════════════════════════════════
    // AC1 — MudBlazor NuGet package in ScrambleCoin.Web.csproj
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>ScrambleCoin.Web.csproj contains a MudBlazor PackageReference.</summary>
    [Fact]
    public void Csproj_ContainsMudBlazorPackageReference()
    {
        var csprojPath = Path.Combine(WebProjectDir, "ScrambleCoin.Web.csproj");
        var content = File.ReadAllText(csprojPath);

        Assert.Contains(
            "MudBlazor",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The MudBlazor package reference is pinned to version 9.4.0.</summary>
    [Fact]
    public void Csproj_MudBlazorPackageIsPinnedToVersion9_4_0()
    {
        var csprojPath = Path.Combine(WebProjectDir, "ScrambleCoin.Web.csproj");
        var content = File.ReadAllText(csprojPath);

        Assert.Contains(
            "9.4.0",
            content,
            StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC2 — AddMudServices() in Program.cs
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Program.cs imports the MudBlazor.Services namespace.</summary>
    [Fact]
    public void ProgramCs_ImportsUsingMudBlazorServices()
    {
        var programPath = Path.Combine(WebProjectDir, "Program.cs");
        var content = File.ReadAllText(programPath);

        Assert.Contains(
            "using MudBlazor.Services;",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>Program.cs calls AddMudServices() to register MudBlazor in the DI container.</summary>
    [Fact]
    public void ProgramCs_CallsAddMudServices()
    {
        var programPath = Path.Combine(WebProjectDir, "Program.cs");
        var content = File.ReadAllText(programPath);

        Assert.Contains(
            "AddMudServices()",
            content,
            StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC3 — CSS / JS references in _Host.cshtml
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>_Host.cshtml loads the Google Roboto font used by MudBlazor's typography.</summary>
    [Fact]
    public void HostCshtml_ContainsRobotoFontLink()
    {
        var hostPath = Path.Combine(WebProjectDir, "Pages", "_Host.cshtml");
        var content = File.ReadAllText(hostPath);

        Assert.Contains(
            "Roboto",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>_Host.cshtml links the MudBlazor CSS bundle.</summary>
    [Fact]
    public void HostCshtml_ContainsMudBlazorMinCssLink()
    {
        var hostPath = Path.Combine(WebProjectDir, "Pages", "_Host.cshtml");
        var content = File.ReadAllText(hostPath);

        Assert.Contains(
            "MudBlazor.min.css",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>_Host.cshtml loads the MudBlazor JavaScript bundle.</summary>
    [Fact]
    public void HostCshtml_ContainsMudBlazorMinJsScript()
    {
        var hostPath = Path.Combine(WebProjectDir, "Pages", "_Host.cshtml");
        var content = File.ReadAllText(hostPath);

        Assert.Contains(
            "MudBlazor.min.js",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>The MudBlazor JS script tag appears after the Blazor server script in _Host.cshtml.</summary>
    [Fact]
    public void HostCshtml_MudBlazorJsLoadedAfterBlazorServerJs()
    {
        var hostPath = Path.Combine(WebProjectDir, "Pages", "_Host.cshtml");
        var content = File.ReadAllText(hostPath);

        var blazorIndex = content.IndexOf("blazor.server.js", StringComparison.Ordinal);
        var mudJsIndex = content.IndexOf("MudBlazor.min.js", StringComparison.Ordinal);

        Assert.True(blazorIndex >= 0, "'blazor.server.js' not found in _Host.cshtml.");
        Assert.True(mudJsIndex >= 0, "'MudBlazor.min.js' not found in _Host.cshtml.");
        Assert.True(
            mudJsIndex > blazorIndex,
            "MudBlazor.min.js must appear after blazor.server.js in _Host.cshtml.");
    }

    /// <summary>The MudBlazor CSS is served from the _content/MudBlazor static assets path.</summary>
    [Fact]
    public void HostCshtml_MudBlazorCssUsesContentPath()
    {
        var hostPath = Path.Combine(WebProjectDir, "Pages", "_Host.cshtml");
        var content = File.ReadAllText(hostPath);

        Assert.Contains(
            "_content/MudBlazor/MudBlazor.min.css",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>The MudBlazor JS is served from the _content/MudBlazor static assets path.</summary>
    [Fact]
    public void HostCshtml_MudBlazorJsUsesContentPath()
    {
        var hostPath = Path.Combine(WebProjectDir, "Pages", "_Host.cshtml");
        var content = File.ReadAllText(hostPath);

        Assert.Contains(
            "_content/MudBlazor/MudBlazor.min.js",
            content,
            StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC4 — @using MudBlazor in _Imports.razor
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>_Imports.razor contains the global MudBlazor namespace import.</summary>
    [Fact]
    public void ImportsRazor_ContainsUsingMudBlazor()
    {
        var importsPath = Path.Combine(WebProjectDir, "_Imports.razor");
        var content = File.ReadAllText(importsPath);

        Assert.Contains(
            "@using MudBlazor",
            content,
            StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC5 — Provider components in MainLayout.razor
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>MainLayout.razor includes MudThemeProvider to apply the dark theme.</summary>
    [Fact]
    public void MainLayout_ContainsMudThemeProvider()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "<MudThemeProvider",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>MainLayout.razor includes MudPopoverProvider for MudBlazor popover support.</summary>
    [Fact]
    public void MainLayout_ContainsMudPopoverProvider()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "<MudPopoverProvider",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>MainLayout.razor includes MudDialogProvider to host modal dialogs.</summary>
    [Fact]
    public void MainLayout_ContainsMudDialogProvider()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "<MudDialogProvider",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>MainLayout.razor includes MudSnackbarProvider to display toast notifications.</summary>
    [Fact]
    public void MainLayout_ContainsMudSnackbarProvider()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "<MudSnackbarProvider",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>MainLayout.razor enables dark mode on MudThemeProvider.</summary>
    [Fact]
    public void MainLayout_MudThemeProvider_HasIsDarkModeTrue()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "IsDarkMode=\"true\"",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>MainLayout.razor defines a dark-mode primary colour (purple #7C4DFF).</summary>
    [Fact]
    public void MainLayout_DarkTheme_HasPurplePrimaryColour()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "#7C4DFF",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>MainLayout.razor defines a dark background colour (#121212).</summary>
    [Fact]
    public void MainLayout_DarkTheme_HasDarkBackground()
    {
        var layoutPath = Path.Combine(WebProjectDir, "Shared", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        Assert.Contains(
            "#121212",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AC6 — MudBlazor-styled landing page in Index.razor
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Index.razor uses MudContainer as its top-level layout component.</summary>
    [Fact]
    public void IndexRazor_ContainsMudContainer()
    {
        var indexPath = Path.Combine(WebProjectDir, "Pages", "Index.razor");
        var content = File.ReadAllText(indexPath);

        Assert.Contains(
            "<MudContainer",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>Index.razor uses MudText for typography.</summary>
    [Fact]
    public void IndexRazor_ContainsMudText()
    {
        var indexPath = Path.Combine(WebProjectDir, "Pages", "Index.razor");
        var content = File.ReadAllText(indexPath);

        Assert.Contains(
            "<MudText",
            content,
            StringComparison.Ordinal);
    }

    /// <summary>Index.razor uses MudPaper for a card-style content panel.</summary>
    [Fact]
    public void IndexRazor_ContainsMudPaper()
    {
        var indexPath = Path.Combine(WebProjectDir, "Pages", "Index.razor");
        var content = File.ReadAllText(indexPath);

        Assert.Contains(
            "<MudPaper",
            content,
            StringComparison.Ordinal);
    }

}
