using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class UpdateExpenseCategoryEndpoint
{
    private const string Name = "UpdateExpenseCategory";

    public static IEndpointRouteBuilder MapUpdateExpenseCategory(this IEndpointRouteBuilder app)
    {
        // Rename or (de)activate.
        app.MapPut(ApiEndpoints.ExpenseCategories.Update, async (
                Guid id,
                UpdateExpenseCategoryRequest request,
                IExpenseCategoryService expenseCategoryService,
                CancellationToken token) =>
            {
                var result = await expenseCategoryService.UpdateAsync(request.MapToExpenseCategoryChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExpenseCategoryResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
