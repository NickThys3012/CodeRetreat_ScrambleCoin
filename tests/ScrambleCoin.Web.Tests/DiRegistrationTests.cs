using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScrambleCoin.Infrastructure.Persistence;

namespace ScrambleCoin.Web.Tests;

/// <summary>
/// Verifies that key services are correctly registered in the ASP.NET Core DI container
/// (acceptance criteria 5). Uses <see cref="WebApplicationFactory{T}"/> so the full
/// <c>ScrambleCoin.Api Program.cs</c> pipeline runs; SQL Server is replaced with an InMemory provider
/// to avoid needing a real database connection.
/// </summary>
public class DiRegistrationTests : IClassFixture<DiRegistrationTests.TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DiRegistrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Custom factory that swaps SQL Server for an in-memory EF Core provider
    /// and suppresses the Application Insights telemetry to keep tests self-contained.
    /// </summary>
    public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove every DbContext-related registration added by Program.cs
                var descriptorsToRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ScrambleCoinDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(ScrambleCoinDbContext))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                    services.Remove(descriptor);

                // Re-register with an InMemory database so no SQL Server is required
                services.AddDbContext<ScrambleCoinDbContext>(options =>
                    options.UseInMemoryDatabase("WebTestDb"));
            });

            // Use a non-conflicting URL to avoid port clashes when tests run in parallel
            builder.UseUrls("http://127.0.0.1:0");
        }
    }

    // ── Acceptance criterion 5: MediatR registered ─────────────────────────
    [Fact]
    public void MediatR_IsRegistered_InDependencyInjectionContainer()
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetService<IMediator>();

        Assert.NotNull(mediator);
    }

    // ── Acceptance criterion 5: EF Core DbContext registered ────────────────
    [Fact]
    public void ScrambleCoinDbContext_IsRegistered_InDependencyInjectionContainer()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetService<ScrambleCoinDbContext>();

        Assert.NotNull(context);
    }

    // ── Health endpoint sanity check ─────────────────────────────────────────
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 2xx but got {(int)response.StatusCode} {response.StatusCode}");
    }

    // ── MediatR ISender also resolvable ─────────────────────────────────────
    [Fact]
    public void MediatR_ISender_IsRegistered_InDependencyInjectionContainer()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetService<ISender>();

        Assert.NotNull(sender);
    }
}
