using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Locations;

public static class UpdateLocationEndpoint
{
    private const string Name = "UpdateLocation";

    public static IEndpointRouteBuilder MapUpdateLocation(this IEndpointRouteBuilder app)
    {
        // Rename or (de)activate. Deactivating needs the location empty and another one of its kind.
        app.MapPut(ApiEndpoints.Locations.Update, async (
                Guid id,
                UpdateLocationRequest request,
                ILocationService locationService,
                CancellationToken token) =>
            {
                var result = await locationService.UpdateAsync(request.MapToLocationChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<LocationResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
