using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Taxes;

public static class UpdateTaxEndpoint
{
    private const string Name = "UpdateTax";

    public static IEndpointRouteBuilder MapUpdateTax(this IEndpointRouteBuilder app)
    {
        // Rename, change the rate or the base, or (de)activate. Logged in the audit (RF-DAT-08).
        app.MapPut(ApiEndpoints.Taxes.Update, async (
                Guid id,
                UpdateTaxRequest request,
                ITaxService taxService,
                CancellationToken token) =>
            {
                var result = await taxService.UpdateAsync(request.MapToTaxChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<TaxResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
