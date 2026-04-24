using Microsoft.Playwright;

namespace ScrambleCoin.E2E.Tests;

/// <summary>
/// Placeholder E2E tests — verifies the E2E test project is correctly configured
/// and that Playwright can launch a browser.
///
/// Prerequisites:
///   Run 'playwright install' (or 'pwsh bin/Debug/net9.0/playwright.ps1 install')
///   before executing these tests for the first time.
/// </summary>
public class SetupTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();
    }

    [Fact]
    public void Setup_IsValid()
    {
        // This test exists to confirm the E2E.Tests project is correctly wired up.
        // Real Playwright UI tests will be added per feature.
        Assert.True(true);
    }

    [Fact]
    public async Task Playwright_CanLaunchBrowser()
    {
        // Confirm a browser can be launched and a new page created.
        Assert.NotNull(_browser);

        var page = await _browser.NewPageAsync();
        Assert.NotNull(page);

        await page.CloseAsync();
    }
}
