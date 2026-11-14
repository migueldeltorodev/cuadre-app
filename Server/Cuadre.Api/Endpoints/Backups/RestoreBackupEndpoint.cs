using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Backups;

/// <summary>
/// RF-DAT-05. Replaces ALL the data with the chosen copy, after leaving a copy of what is there now.
/// The client asks for confirmation first and reloads the whole page after.
/// </summary>
public static class RestoreBackupEndpoint
{
    private const string Name = "RestoreBackup";

    public static IEndpointRouteBuilder MapRestoreBackup(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Backups.Restore, async (
                RestoreBackupRequest request,
                IBackupService backupService,
                CancellationToken token) =>
            {
                var result = await backupService.RestoreAsync(request.Location.MapToBackupLocation(), request.FileName,
                    token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<RestoreResultResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
