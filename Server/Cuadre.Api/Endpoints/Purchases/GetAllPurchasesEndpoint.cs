using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Purchases;

public static class GetAllPurchasesEndpoint
{
    private const string Name = "GetAllPurchases";

    public static IEndpointRouteBuilder MapGetAllPurchases(this IEndpointRouteBuilder app)
    {
        // ?from=2026-11-01&to=2026-11-30&supplierId=...&productId=...&categoryId=...&includeVoided=true
        app.MapGet(ApiEndpoints.Purchases.GetAll, async (
                [AsParameters] GetAllPurchasesRequest request,
                IPurchaseService purchaseService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var purchases = await purchaseService.GetAllAsync(options, token);
                var total = await purchaseService.GetCountAsync(options, token);
                return TypedResults.Ok(purchases.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<PurchasesResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
