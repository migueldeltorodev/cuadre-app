using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class CreateExpenseCategoryEndpoint
{
    private const string Name = "CreateExpenseCategory";

    public static IEndpointRouteBuilder MapCreateExpenseCategory(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.ExpenseCategories.Create, async (
                CreateExpenseCategoryRequest request,
                IExpenseCategoryService expenseCategoryService,
                CancellationToken token) =>
            {
                var result = await expenseCategoryService.CreateAsync(request.MapToExpenseCategory(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.ExpenseCategories.GetAll}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExpenseCategoryResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
