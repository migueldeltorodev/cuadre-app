using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Settings;

public static class UpdateBusinessSettingsEndpoint
{
    private const string Name = "UpdateBusinessSettings";

    public static IEndpointRouteBuilder MapUpdateBusinessSettings(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.Settings.Update, async (
                UpdateBusinessSettingsRequest request,
                IBusinessSettingsService settingsService,
                CancellationToken token) =>
            {
                var updated = await settingsService.UpdateAsync(request.MapToBusinessSettings(), token);
                return TypedResults.Ok(updated.MapToResponse());
            })
            .WithName(Name)
            .Produces<BusinessSettingsResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
