using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class GetDueExpensesEndpoint
{
    private const string Name = "GetDueExpenses";

    public static IEndpointRouteBuilder MapGetDueExpenses(this IEndpointRouteBuilder app)
    {
        // What the app proposes to pay: the occurrences due up to today, not confirmed nor skipped.
        app.MapGet(ApiEndpoints.RecurringExpenses.Due, async (
                IRecurringExpenseService recurringExpenseService,
                CancellationToken token) =>
            {
                var due = await recurringExpenseService.GetDueAsync(token);
                return TypedResults.Ok(due.MapToResponse());
            })
            .WithName(Name)
            .Produces<DueExpensesResponse>();

        return app;
    }
}
