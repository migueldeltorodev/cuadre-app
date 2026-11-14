using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Taxes;

public static class CreateTaxEndpoint
{
    private const string Name = "CreateTax";

    public static IEndpointRouteBuilder MapCreateTax(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Taxes.Create, async (
                CreateTaxRequest request,
                ITaxService taxService,
                CancellationToken token) =>
            {
                var result = await taxService.CreateAsync(request.MapToTax(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.Taxes.GetAll}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<TaxResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
