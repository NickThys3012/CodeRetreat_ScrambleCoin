using Microsoft.Playwright;

namespace ScrambleCoin.E2E.Tests;

public class VillainManagerE2ETests : IAsyncLifetime
{
    private const string AppBaseUrl = "http://localhost:5026";
    private const string PageUrl = $"{AppBaseUrl}/admin/villains";

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page is not null) await _page.CloseAsync();
        if (_context is not null) await _context.DisposeAsync();
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task VillainManager_PageLoads()
    {
        await _page!.GotoAsync(PageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await _page.WaitForTimeoutAsync(2000);

        var header = await _page.GetByText("🎭 Villain Tree Manager").IsVisibleAsync();
        Assert.True(header, "Villain Manager header should be visible");
    }

    [Fact]
    public async Task VillainManager_SeededNodesAreVisible()
    {
        await _page!.GotoAsync(PageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await _page.WaitForTimeoutAsync(2000);

        // Seeded villains appear in the right-panel flat list as <strong> tags
        var stitchVisible = await _page.GetByText("Stitch").First.IsVisibleAsync();
        Assert.True(stitchVisible, "Seeded villain 'Stitch' should be visible in the list");
    }

    [Fact]
    public async Task VillainManager_AddButtonOpensDialog()
    {
        await _page!.GotoAsync(PageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await _page.WaitForTimeoutAsync(2000);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Add Villain" }).ClickAsync();
        await _page.WaitForTimeoutAsync(1000);

        var dialogOpen = await _page.Locator("text=Add Villain Node").IsVisibleAsync();
        Assert.True(dialogOpen, "Add dialog should open after clicking Add Villain");
    }

    [Fact]
    public async Task VillainManager_CancelClosesDialog()
    {
        await _page!.GotoAsync(PageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await _page.WaitForTimeoutAsync(2000);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Add Villain" }).ClickAsync();
        await _page.WaitForTimeoutAsync(1000);

        Assert.True(await _page.Locator("text=Add Villain Node").IsVisibleAsync(), "Dialog should be open");

        await _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        var stillOpen = await _page.Locator("text=Add Villain Node").IsVisibleAsync();
        Assert.False(stillOpen, "Dialog should be closed after Cancel");
    }

    [Fact]
    public async Task VillainManager_AddVillainAndVerifySaved()
    {
        // Use a villain from the catalogue that is not in the seeder
        const string testVillain = "Hades";

        await _page!.GotoAsync(PageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await _page.WaitForTimeoutAsync(2000);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Add Villain" }).ClickAsync();
        await _page.WaitForTimeoutAsync(1000);

        // The villain name is now a MudSelect dropdown (label "Villain")
        // Click the select input to open the dropdown
        await _page.Locator("div[role='dialog'] input[readonly]").First.ClickAsync();
        await _page.WaitForTimeoutAsync(600);

        // Pick the villain from the open list
        await _page.Locator("[role='option']").GetByText(testVillain, new LocatorGetByTextOptions { Exact = true }).ClickAsync();
        await _page.WaitForTimeoutAsync(500);

        // Click Save
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await _page.WaitForTimeoutAsync(2000);

        // Villain should appear in the right-panel list (or was already there from a prior run)
        var visible = await _page.GetByText(testVillain).First.IsVisibleAsync();
        Assert.True(visible, $"Villain '{testVillain}' should be visible after save");
    }
}
