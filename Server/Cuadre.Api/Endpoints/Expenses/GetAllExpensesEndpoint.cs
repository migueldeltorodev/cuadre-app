using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class GetAllExpensesEndpoint
{
    private const string Name = "GetAllExpenses";

    public static IEndpointRouteBuilder MapGetAllExpenses(this IEndpointRouteBuilder app)
    {
        // ?from=...&to=...&categoryId=...&cashBoxId=...&envelopeId=...&recurringExpenseId=...&includeVoided=true
        app.MapGet(ApiEndpoints.Expenses.GetAll, async (
                [AsParameters] GetAllExpensesRequest request,
                IExpenseService expenseService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var expenses = await expenseService.GetAllAsync(options, token);
                var total = await expenseService.GetCountAsync(options, token);
                var totals = await expenseService.GetTotalsAsync(options, token);
                return TypedResults.Ok(expenses.MapToResponse(totals, options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<ExpensesResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
