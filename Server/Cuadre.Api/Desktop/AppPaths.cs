namespace Cuadre.Api.Desktop;

/// <summary>
/// Where Cuadre keeps its files on the PC. Default: %LOCALAPPDATA%\Cuadre.
/// Overridable with "App:DataDirectory" (Development points it to the repo's .data folder,
/// so test data never mixes with the real business database).
/// </summary>
public sealed class AppPaths
{
    public AppPaths(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["App:DataDirectory"];
        Root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cuadre")
            : Path.GetFullPath(configured, environment.ContentRootPath);
    }

    public string Root { get; }
    public string DatabaseFile => Path.Combine(Root, "data", "cuadre.db");
    public string Logs => Path.Combine(Root, "logs");
    public string Backups => Path.Combine(Root, "backups");
    public string EdgeProfile => Path.Combine(Root, "edge-profile");
}
