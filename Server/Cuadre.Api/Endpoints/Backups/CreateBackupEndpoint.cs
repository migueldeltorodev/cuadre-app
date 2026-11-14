using Cuadre.Api.Mapping;
using Cuadre.Application.Models;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Backups;

/// <summary>"Respaldar ahora". Waits for the copy (seconds): the owner wants to see it done.</summary>
public static class CreateBackupEndpoint
{
    private const string Name = "CreateBackup";

    public static IEndpointRouteBuilder MapCreateBackup(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Backups.Create, async (
                IBackupService backupService,
                CancellationToken token) =>
            {
                var result = await backupService.CreateAsync(BackupReason.Manual, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<BackupResultResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
