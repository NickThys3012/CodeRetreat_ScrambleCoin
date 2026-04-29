using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Infrastructure.Persistence;

// ── Serilog bootstrap logger (catches startup errors) ────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ScrambleCoin web host");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog full logger ───────────────────────────────────────────────────
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/scramblecoin-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);

        // Conditionally add Application Insights sink when connection string is present
        var aiConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(aiConnectionString))
        {
            configuration.WriteTo.ApplicationInsights(
                aiConnectionString,
                TelemetryConverter.Traces);
        }
    });

    // ── Blazor Server ─────────────────────────────────────────────────────────
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    // ── MudBlazor ─────────────────────────────────────────────────────────────
    builder.Services.AddMudServices();

    // ── MediatR ───────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(
            typeof(ScrambleCoin.Application.Placeholder).Assembly));

    // ── Application services ──────────────────────────────────────────────────
    builder.Services.AddSingleton(Random.Shared);
    builder.Services.AddScoped<IGameRepository, GameRepository>();

    // ── EF Core (SQL Server) ──────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=(localdb)\\mssqllocaldb;Database=ScrambleCoin;Trusted_Connection=True;";

    builder.Services.AddDbContext<ScrambleCoinDbContext>(options =>
        options.UseSqlServer(connectionString));

    // ── Application Insights (telemetry) ─────────────────────────────────────
    builder.Services.AddApplicationInsightsTelemetry();

    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    app.UseSerilogRequestLogging();

    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    // Minimal API health check
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ScrambleCoin host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Needed for WebApplicationFactory in integration tests
public partial class Program { }

