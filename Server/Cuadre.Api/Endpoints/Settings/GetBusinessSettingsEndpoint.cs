using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Settings;

public static class GetBusinessSettingsEndpoint
{
    public const string Name = "GetBusinessSettings";

    public static IEndpointRouteBuilder MapGetBusinessSettings(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Settings.Get, async (
                IBusinessSettingsService settingsService,
                CancellationToken token) =>
            {
                var settings = await settingsService.GetAsync(token);
                return TypedResults.Ok(settings.MapToResponse());
            })
            .WithName(Name)
            .Produces<BusinessSettingsResponse>();

        return app;
    }
}
