using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Expenses;

public static class GetAllExpenseCategoriesEndpoint
{
    private const string Name = "GetAllExpenseCategories";

    public static IEndpointRouteBuilder MapGetAllExpenseCategories(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.ExpenseCategories.GetAll, async (
                IExpenseCategoryService expenseCategoryService,
                CancellationToken token) =>
            {
                var categories = await expenseCategoryService.GetAllAsync(token);
                return TypedResults.Ok(categories.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExpenseCategoriesResponse>();

        return app;
    }
}
