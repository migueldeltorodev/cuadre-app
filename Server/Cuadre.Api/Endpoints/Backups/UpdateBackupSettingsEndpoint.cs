using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Backups;

/// <summary>The pendrive folder and the alert days. Returns the whole status: the screen redraws from it.</summary>
public static class UpdateBackupSettingsEndpoint
{
    private const string Name = "UpdateBackupSettings";

    public static IEndpointRouteBuilder MapUpdateBackupSettings(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.Backups.Settings, async (
                UpdateBackupSettingsRequest request,
                IBackupService backupService,
                CancellationToken token) =>
            {
                var result = await backupService.UpdateSettingsAsync(request.MapToBackupSettings(), token);
                if (result.IsFailed)
                {
                    // Only the failure is used: it becomes the 422 ProblemDetails.
                    return result.ToResult().ToNoContentResult();
                }

                var status = await backupService.GetStatusAsync(token);
                return (IResult)TypedResults.Ok(status.MapToResponse());
            })
            .WithName(Name)
            .Produces<BackupStatusResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
