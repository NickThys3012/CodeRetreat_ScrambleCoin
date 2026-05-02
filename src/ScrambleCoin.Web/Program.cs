using MudBlazor.Services;
using Serilog;
using Serilog.Events;

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

