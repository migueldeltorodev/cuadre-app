using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Backups;

/// <summary>The pendrives plugged in right now, so the owner picks one instead of typing "E:\".</summary>
public static class GetRemovableDrivesEndpoint
{
    private const string Name = "GetRemovableDrives";

    public static IEndpointRouteBuilder MapGetRemovableDrives(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Backups.Drives, (IBackupService backupService) =>
                TypedResults.Ok(backupService.GetRemovableDrives().MapToResponse()))
            .WithName(Name)
            .Produces<RemovableDrivesResponse>();

        return app;
    }
}
