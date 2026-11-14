using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class CreateExpenseEndpoint
{
    private const string Name = "CreateExpense";

    public static IEndpointRouteBuilder MapCreateExpense(this IEndpointRouteBuilder app)
    {
        // Leaving the envelope negative is allowed, with a warning (RF-PF-08).
        app.MapPost(ApiEndpoints.Expenses.Create, async (
                CreateExpenseRequest request,
                IExpenseService expenseService,
                CancellationToken token) =>
            {
                var result = await expenseService.CreateAsync(request.MapToExpense(), token);
                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetExpenseEndpoint.Name,
                    x => new { id = x.Expense.Id });
            })
            .WithName(Name)
            .Produces<ExpenseResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
