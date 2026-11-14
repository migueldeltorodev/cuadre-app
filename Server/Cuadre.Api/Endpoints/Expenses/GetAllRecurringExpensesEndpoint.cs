using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class GetAllRecurringExpensesEndpoint
{
    private const string Name = "GetAllRecurringExpenses";

    public static IEndpointRouteBuilder MapGetAllRecurringExpenses(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.RecurringExpenses.GetAll, async (
                IRecurringExpenseService recurringExpenseService,
                CancellationToken token) =>
            {
                var recurring = await recurringExpenseService.GetAllAsync(token);
                return TypedResults.Ok(recurring.MapToResponse());
            })
            .WithName(Name)
            .Produces<RecurringExpensesResponse>();

        return app;
    }
}
