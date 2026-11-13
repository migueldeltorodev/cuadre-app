using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class ConfirmRecurringExpenseEndpoint
{
    private const string Name = "ConfirmRecurringExpense";

    public static IEndpointRouteBuilder MapConfirmRecurringExpense(this IEndpointRouteBuilder app)
    {
        // Creates the expense of one occurrence (201 at the expense).
        app.MapPost(ApiEndpoints.RecurringExpenses.Confirm, async (
                Guid id,
                ConfirmRecurringExpenseRequest request,
                IRecurringExpenseService recurringExpenseService,
                CancellationToken token) =>
            {
                var result = await recurringExpenseService.ConfirmAsync(
                    request.MapToConfirmRecurringExpense(id), token);
                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetExpenseEndpoint.Name,
                    x => new { id = x.Expense.Id });
            })
            .WithName(Name)
            .Produces<ExpenseResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
