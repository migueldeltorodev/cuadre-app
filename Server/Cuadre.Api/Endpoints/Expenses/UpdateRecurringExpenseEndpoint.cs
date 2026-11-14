using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class UpdateRecurringExpenseEndpoint
{
    private const string Name = "UpdateRecurringExpense";

    public static IEndpointRouteBuilder MapUpdateRecurringExpense(this IEndpointRouteBuilder app)
    {
        // Everything, (de)activating included. The expenses already confirmed keep their own data.
        app.MapPut(ApiEndpoints.RecurringExpenses.Update, async (
                Guid id,
                UpdateRecurringExpenseRequest request,
                IRecurringExpenseService recurringExpenseService,
                CancellationToken token) =>
            {
                var result = await recurringExpenseService.UpdateAsync(request.MapToRecurringExpense(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<RecurringExpenseResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
