using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using ScrambleCoin.Application.BotRegistration;
using ScrambleCoin.Application.Interfaces;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Application.Services.Villains;
using ScrambleCoin.Infrastructure.Persistence;
using ScrambleCoin.Infrastructure.Services;
using ScrambleCoin.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
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

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(ScrambleCoin.Application.Games.CreateGame.CreateGameCommandHandler).Assembly);
    // Serialization must wrap everything — register first so it is the outermost behaviour.
    cfg.AddOpenBehavior(typeof(ScrambleCoin.Application.Behaviours.GameSerializationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(ScrambleCoin.Application.Behaviours.SignalRBroadcastBehaviour<,>));
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddSingleton(Random.Shared);
builder.Services.AddSingleton<GameLockService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<ScrambleCoin.Application.Abstractions.IGameBroadcaster,
    ScrambleCoin.Api.Hubs.GameBroadcaster>();
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IBotRegistrationRepository, BotRegistrationRepository>();
builder.Services.AddScoped<IVillainTreeRepository, VillainTreeRepository>();
builder.Services.AddScoped<IBotUnlocksRepository, BotUnlocksRepository>();
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IRankingRepository, RankingRepository>();
builder.Services.AddScoped<ScrambleCoin.Application.Games.Replay.IGameSnapshotRepository,
    GameSnapshotRepository>();
builder.Services.AddScoped<ICoinSpawnService, CoinSpawnService>();
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ScrambleCoinDbContext>());
builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection("Queue"));
builder.Services.AddSingleton<IQueueService, QueueService>();

// ── Villain services ──────────────────────────────────────────────────────────
builder.Services.AddScoped<IVillainStrategyFactory, VillainStrategyFactory>();
builder.Services.AddScoped<IVillainActionDispatcher, VillainActionDispatcher>();
builder.Services.AddScoped<IVillainAutomationService, VillainAutomationService>();

// ── EF Core (SQL Server) ──────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=ScrambleCoin;Trusted_Connection=True;";

builder.Services.AddDbContext<ScrambleCoinDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── Application Insights (telemetry) ─────────────────────────────────────────
builder.Services.AddApplicationInsightsTelemetry();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ScrambleCoinDbContext>("database");

// ── OpenAPI / Swagger ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ScrambleCoin Bot API",
        Version = "v1",
        Description = "REST API for Scramblecoin bots — create games, register bots, submit moves."
    });

    // Admin key security scheme (lock icon on CreateGame)
    options.AddSecurityDefinition("X-Admin-Key", new OpenApiSecurityScheme
    {
        Name = "X-Admin-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Admin key required to create a game shell. Value: `scramblecoin-admin`"
    });

    options.OperationFilter<ScrambleCoin.Api.Swagger.AdminKeyOperationFilter>();
});

var app = builder.Build();

// ── Database initialization ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ScrambleCoinDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    VillainTreeSeeder.SeedDefaultTree(dbContext);
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSerilogRequestLogging();

// ── Swagger UI (all environments for the event) ───────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrambleCoin Bot API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/health", async (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    var result = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(
            e => e.Key,
            e => e.Value.Status.ToString())
    };
    return report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
        ? Results.Ok(result)
        : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithName("HealthCheck")
.WithSummary("Health check")
.WithDescription("Returns 200 Healthy when the API and database are reachable, or 503 Unhealthy if a dependency is down.")
.WithTags("Health")
.Produces<object>()
.Produces<object>(StatusCodes.Status503ServiceUnavailable);
app.MapGameEndpoints();
app.MapSoloModeEndpoints();
app.MapTournamentEndpoints();
app.MapHub<ScrambleCoin.Api.Hubs.GameHub>("/hubs/game");

app.Run();


