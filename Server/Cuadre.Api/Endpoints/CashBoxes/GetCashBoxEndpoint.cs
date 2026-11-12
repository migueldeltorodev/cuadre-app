using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashBoxes;

public static class GetCashBoxEndpoint
{
    public const string Name = "GetCashBox";

    public static IEndpointRouteBuilder MapGetCashBox(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.CashBoxes.Get, async (
                Guid id,
                ICashBoxService cashBoxService,
                CancellationToken token) =>
            {
                var result = await cashBoxService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<CashBoxResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
