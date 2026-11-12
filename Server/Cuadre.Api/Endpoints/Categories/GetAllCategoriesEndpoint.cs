using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Categories;

public static class GetAllCategoriesEndpoint
{
    private const string Name = "GetAllCategories";

    public static IEndpointRouteBuilder MapGetAllCategories(this IEndpointRouteBuilder app)
    {
        // Flat list (top-level first) with product counts; the UI builds the tree.
        app.MapGet(ApiEndpoints.Categories.GetAll, async (
                ICategoryService categoryService,
                CancellationToken token) =>
            {
                var items = await categoryService.GetAllAsync(token);
                return TypedResults.Ok(items.MapToResponse());
            })
            .WithName(Name)
            .Produces<CategoriesResponse>();

        return app;
    }
}
