using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Locations;

public static class GetLocationEndpoint
{
    public const string Name = "GetLocation";

    public static IEndpointRouteBuilder MapGetLocation(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Locations.Get, async (
                Guid id,
                ILocationService locationService,
                CancellationToken token) =>
            {
                var result = await locationService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<LocationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
