using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class GetExpenseEndpoint
{
    public const string Name = "GetExpense";

    public static IEndpointRouteBuilder MapGetExpense(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Expenses.Get, async (
                Guid id,
                IExpenseService expenseService,
                CancellationToken token) =>
            {
                var result = await expenseService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
