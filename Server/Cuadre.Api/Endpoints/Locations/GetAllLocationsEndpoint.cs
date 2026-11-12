using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Locations;

public static class GetAllLocationsEndpoint
{
    private const string Name = "GetAllLocations";

    public static IEndpointRouteBuilder MapGetAllLocations(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Locations.GetAll, async (
                ILocationService locationService,
                CancellationToken token) =>
            {
                var locations = await locationService.GetAllAsync(token);
                return TypedResults.Ok(locations.MapToResponse());
            })
            .WithName(Name)
            .Produces<LocationsResponse>();

        return app;
    }
}
