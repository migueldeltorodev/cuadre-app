using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class VoidExpenseEndpoint
{
    private const string Name = "VoidExpense";

    public static IEndpointRouteBuilder MapVoidExpense(this IEndpointRouteBuilder app)
    {
        // POST, not DELETE: the expense stays, marked as voided, with its reversal (RF-GAS-04).
        app.MapPost(ApiEndpoints.Expenses.Void, async (
                Guid id,
                VoidExpenseRequest request,
                IExpenseService expenseService,
                CancellationToken token) =>
            {
                var result = await expenseService.VoidAsync(request.MapToVoidExpense(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExpenseResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
