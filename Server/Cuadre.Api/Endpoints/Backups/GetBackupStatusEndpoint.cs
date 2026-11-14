using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Backups;

public static class GetBackupStatusEndpoint
{
    private const string Name = "GetBackupStatus";

    public static IEndpointRouteBuilder MapGetBackupStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Backups.Status, async (
                IBackupService backupService,
                CancellationToken token) =>
            {
                var status = await backupService.GetStatusAsync(token);
                return TypedResults.Ok(status.MapToResponse());
            })
            .WithName(Name)
            .Produces<BackupStatusResponse>();

        return app;
    }
}
