namespace Cuadre.Api.Endpoints.Backups;

/// <summary>RF-DAT-01..06: the backup screen and the home alert.</summary>
public static class BackupEndpointExtensions
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetBackupStatus();
        app.MapCreateBackup();
        app.MapUpdateBackupSettings();
        app.MapGetRemovableDrives();
        app.MapRestoreBackup();
        return app;
    }
}
