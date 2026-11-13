using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class CreateRecurringExpenseEndpoint
{
    private const string Name = "CreateRecurringExpense";

    public static IEndpointRouteBuilder MapCreateRecurringExpense(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.RecurringExpenses.Create, async (
                CreateRecurringExpenseRequest request,
                IRecurringExpenseService recurringExpenseService,
                CancellationToken token) =>
            {
                var result = await recurringExpenseService.CreateAsync(request.MapToRecurringExpense(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.RecurringExpenses.GetAll}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<RecurringExpenseResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
