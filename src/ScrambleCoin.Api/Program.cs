using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Infrastructure.Persistence;
using ScrambleCoin.Infrastructure.Services;
using ScrambleCoin.Api.Endpoints;

// ── Serilog bootstrap logger (catches startup errors) ────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ScrambleCoin API host");

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
                path: "logs/scramblecoin-api-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);

        var aiConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(aiConnectionString))
        {
            configuration.WriteTo.ApplicationInsights(
                aiConnectionString,
                TelemetryConverter.Traces);
        }
    });

    // ── MediatR ───────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(
            typeof(ScrambleCoin.Application.Games.PlacePiece.PlacePieceCommandHandler).Assembly));

    // ── Application services ──────────────────────────────────────────────────
    builder.Services.AddSingleton(Random.Shared);
    builder.Services.AddScoped<IGameRepository, GameRepository>();
    builder.Services.AddScoped<IBotRegistrationRepository, BotRegistrationRepository>();
    builder.Services.AddScoped<ICoinSpawnService, CoinSpawnService>();
    builder.Services.AddSingleton<IQueueService, QueueService>();

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
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseSerilogRequestLogging();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
    app.MapGameEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ScrambleCoin API host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
