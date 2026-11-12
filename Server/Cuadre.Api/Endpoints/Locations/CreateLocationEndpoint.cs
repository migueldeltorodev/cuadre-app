using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Locations;

public static class CreateLocationEndpoint
{
    private const string Name = "CreateLocation";

    public static IEndpointRouteBuilder MapCreateLocation(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Locations.Create, async (
                CreateLocationRequest request,
                ILocationService locationService,
                CancellationToken token) =>
            {
                var result = await locationService.CreateAsync(request.MapToLocation(), token);
                return result.ToCreatedAtRouteResult(x => x.MapToResponse(), GetLocationEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<LocationResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
