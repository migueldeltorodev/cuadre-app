using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Taxes;

public static class GetAllTaxesEndpoint
{
    private const string Name = "GetAllTaxes";

    public static IEndpointRouteBuilder MapGetAllTaxes(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Taxes.GetAll, async (
                ITaxService taxService,
                CancellationToken token) =>
            {
                var taxes = await taxService.GetAllAsync(token);
                return TypedResults.Ok(taxes.MapToResponse());
            })
            .WithName(Name)
            .Produces<TaxesResponse>();

        return app;
    }
}
