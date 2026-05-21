using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;

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

    // ── MudBlazor ─────────────────────────────────────────────────────────────
    builder.Services.AddMudServices();

    // ── MediatR ───────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(
            typeof(ScrambleCoin.Application.Games.CreateGame.CreateGameCommandHandler).Assembly));

    // ── Database & EF Core ─────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ScrambleCoin.Infrastructure.Persistence.ScrambleCoinDbContext>(opts =>
        opts.UseSqlServer(connectionString));

    // ── Repositories ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IGameRepository,
        ScrambleCoin.Infrastructure.Persistence.GameRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IBotUnlocksRepository,
        ScrambleCoin.Infrastructure.BotUnlocksRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.Interfaces.IVillainTreeRepository,
        ScrambleCoin.Infrastructure.VillainTreeRepository>();
    builder.Services.AddScoped<ScrambleCoin.Application.BotRegistration.IBotRegistrationRepository,
        ScrambleCoin.Infrastructure.Persistence.BotRegistrationRepository>();

    // ── Application Services ───────────────────────────────────────────────────
    builder.Services.AddScoped<ScrambleCoin.Application.Services.ICoinSpawnService,
        ScrambleCoin.Application.Services.CoinSpawnService>();
    builder.Services.AddScoped<ScrambleCoin.Application.Services.IVillainActionDispatcher,
        ScrambleCoin.Application.Services.VillainActionDispatcher>();
    builder.Services.AddScoped<ScrambleCoin.Application.Services.IVillainAutomationService,
        ScrambleCoin.Application.Services.VillainAutomationService>();
    builder.Services.AddSingleton<ScrambleCoin.Application.Services.Villains.IVillainStrategyFactory,
        ScrambleCoin.Application.Services.Villains.VillainStrategyFactory>();
    builder.Services.AddSingleton<System.Random>();
    
    var app = builder.Build();

    // ── Database initialization & seeding ─────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrambleCoin.Infrastructure.Persistence.ScrambleCoinDbContext>();
        dbContext.Database.Migrate();
        ScrambleCoin.Infrastructure.Persistence.VillainTreeSeeder.SeedDefaultTree(dbContext);
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