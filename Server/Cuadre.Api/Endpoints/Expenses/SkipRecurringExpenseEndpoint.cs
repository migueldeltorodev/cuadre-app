using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class SkipRecurringExpenseEndpoint
{
    private const string Name = "SkipRecurringExpense";

    public static IEndpointRouteBuilder MapSkipRecurringExpense(this IEndpointRouteBuilder app)
    {
        // "This time it was not paid": no money moves, the occurrence stops being proposed.
        app.MapPost(ApiEndpoints.RecurringExpenses.Skip, async (
                Guid id,
                SkipRecurringExpenseRequest request,
                IRecurringExpenseService recurringExpenseService,
                CancellationToken token) =>
            {
                var result = await recurringExpenseService.SkipAsync(request.MapToSkipRecurringExpense(id), token);
                return result.ToNoContentResult();
            })
            .WithName(Name)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
