using Cuadre.Api.Mapping;
using Cuadre.Application.Models;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Units;

public static class GetAllUnitsEndpoint
{
    private const string Name = "GetAllUnits";

    public static IEndpointRouteBuilder MapGetAllUnits(this IEndpointRouteBuilder app)
    {
        // Static list from code: no service, no database.
        app.MapGet(ApiEndpoints.Units.GetAll, () =>
                TypedResults.Ok(Enum.GetValues<MeasureUnit>().MapToUnitsResponse()))
            .WithName(Name)
            .Produces<UnitsResponse>();

        return app;
    }
}
