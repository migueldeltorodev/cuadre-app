using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashBoxes;

public static class CreateCashBoxEndpoint
{
    private const string Name = "CreateCashBox";

    public static IEndpointRouteBuilder MapCreateCashBox(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.CashBoxes.Create, async (
                CreateCashBoxRequest request,
                ICashBoxService cashBoxService,
                CancellationToken token) =>
            {
                var result = await cashBoxService.CreateAsync(request.MapToCashBox(), token);

                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetCashBoxEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<CashBoxResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
