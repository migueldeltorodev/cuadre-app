using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashBoxes;

public static class UpdateCashBoxEndpoint
{
    private const string Name = "UpdateCashBox";

    public static IEndpointRouteBuilder MapUpdateCashBox(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.CashBoxes.Update, async (
                Guid id,
                UpdateCashBoxRequest request,
                ICashBoxService cashBoxService,
                CancellationToken token) =>
            {
                var result = await cashBoxService.UpdateAsync(request.MapToCashBoxChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<CashBoxResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
