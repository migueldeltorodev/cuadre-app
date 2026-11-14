using Asp.Versioning;
using Cuadre.Api;
using Cuadre.Api.Desktop;
using Cuadre.Api.Endpoints;
using Cuadre.Api.Health;
using Cuadre.Api.Mapping;
using Cuadre.Api.OpenApi;
using Cuadre.Application;
using Cuadre.Application.Database;
using Cuadre.Application.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var paths = new AppPaths(config, builder.Environment);
builder.Services.AddSingleton(paths);

// Logs to file: in the beta this is how we debug problems on the PC (14 days kept).
builder.Services.AddSerilog((_, logger) => logger
    .ReadFrom.Configuration(config)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(paths.Logs, "cuadre-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

builder.Services.AddApiVersioning(x =>
    {
        x.DefaultApiVersion = new ApiVersion(1.0);
        x.AssumeDefaultVersionWhenUnspecified = true;
        x.ReportApiVersions = true;
        // Clients may send:  Accept: application/json;api-version=1.0
        x.ApiVersionReader = new MediaTypeApiVersionReader("api-version");
    })
    .AddApiExplorer(x => x.GroupNameFormat = "'v'VVV")
    .AddApiDocumentation();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name);

builder.Services.AddOptions<DesktopOptions>().Bind(config.GetSection(DesktopOptions.SectionName));
builder.Services.AddHostedService<DesktopWindowService>();

// No auth and no CORS on purpose: single local user, and Kestrel listens ONLY on 127.0.0.1
// ("Urls" in appsettings.json), so nothing on the Wi-Fi can reach the API.
// In Development the Angular dev server proxies /api to this host (Client/proxy.conf.json).

var connectionString = config["Database:ConnectionString"] is { Length: > 0 } configured
    ? configured
    : $"Data Source={paths.DatabaseFile};Pooling=True";

// Every timestamp the API returns is UTC and serialized with its 'Z'. Enums travel as strings:
// the Contracts enums carry [JsonConverter(typeof(JsonStringEnumConverter<T>))], so clients agree too.

builder.Services.AddApplication();
builder.Services.AddDatabase(connectionString);

// Backups (RF-DAT-01..06): the copies in %LOCALAPPDATA%\Cuadre\backups, their state next to the database (D-56),
// a worker for the automatic ones and enough shutdown time for the copy made when the window closes.
builder.Services.AddBackups(new BackupPaths(paths.Backups, paths.BackupStateFile));
builder.Services.AddOptions<BackupOptions>().Bind(config.GetSection(BackupOptions.SectionName));
builder.Services.AddHostedService<BackupWorker>();
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("_health");

app.UseMiddleware<ValidationMappingMiddleware>();
app.MapApiEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();   // /openapi/v1.json + Scalar UI at /scalar
}

// The Angular build is published into wwwroot. Any non-API route falls back to index.html
// (client-side routing); unknown /api routes still return a real 404.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallback($"{ApiEndpoints.ApiBase}/{{**path}}", () => Results.NotFound());   // more specific: wins for /api/*
app.MapFallbackToFile("index.html");

// Schema first (DbUp migrations), then factory defaults (idempotent). Both BEFORE serving requests.
// D-57: a new version that migrates the database first leaves a copy of it as it was.
await app.Services.GetRequiredService<IBackupService>().CreateBeforeUpdateIfNeededAsync();
await app.Services.GetRequiredService<DbInitializer>().InitializeAsync();
await app.Services.GetRequiredService<DatabaseSeeder>().SeedAsync();

app.Run();
