using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using ScrambleCoin.Application.Abstractions;
using ScrambleCoin.Application.Behaviours;
using ScrambleCoin.Application.Services;
using ScrambleCoin.Infrastructure.Persistence;
using ScrambleCoin.Web.Hubs;

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
                path: "logs/scramblecoin-web-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);
    });

    // ── Blazor Server ─────────────────────────────────────────────────────────
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    // ── SignalR ───────────────────────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ── MudBlazor ─────────────────────────────────────────────────────────────
    builder.Services.AddMudServices();

    // ── MediatR ───────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblies(
            typeof(ScrambleCoin.Application.Games.CreateGame.CreateGameCommandHandler).Assembly);
        // Serialization must wrap everything — register first so it is the outermost behaviour.
        cfg.AddOpenBehavior(typeof(GameSerializationBehaviour<,>));
        cfg.AddOpenBehavior(typeof(SignalRBroadcastBehaviour<,>));
    });
    builder.Services.AddSingleton<GameLockService>();

    // ── Database & EF Core ─────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ScrambleCoinDbContext>(opts =>
        opts.UseSqlServer(connectionString));

    // ── Repositories ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IGameRepository,
        GameRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IBotUnlocksRepository,
        BotUnlocksRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IVillainTreeRepository,
        VillainTreeRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.BotRegistration.IBotRegistrationRepository,
        BotRegistrationRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Tournament.ITournamentRepository,
        TournamentRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IRankingRepository,
        RankingRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IUnitOfWork>(
        sp => sp.GetRequiredService<ScrambleCoinDbContext>());

    // ── Application Services ───────────────────────────────────────────────────
    builder.Services.AddScoped<ICoinSpawnService,
        CoinSpawnService>();
    builder.Services.AddScoped<IVillainActionDispatcher,
        VillainActionDispatcher>();
    builder.Services.AddScoped<IVillainAutomationService,
        VillainAutomationService>();
    builder.Services.AddSingleton<ScrambleCoin.Application.Services.Villains.IVillainStrategyFactory,
        ScrambleCoin.Application.Services.Villains.VillainStrategyFactory>();
    builder.Services.AddSingleton<Random>();
    
    // ── SignalR Broadcaster ───────────────────────────────────────────────────
    builder.Services.AddScoped<IGameBroadcaster, GameBroadcaster>();

    var app = builder.Build();

    // ── Database initialization & seeding ─────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrambleCoinDbContext>();
        dbContext.Database.Migrate();
        VillainTreeSeeder.SeedDefaultTree(dbContext);
    }

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
    app.MapHub<GameHub>("/hubs/game");
    app.MapFallbackToPage("/_Host");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ScrambleCoin web host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


public partial class Program;
