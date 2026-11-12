using Cuadre.Api.Mapping;
using Cuadre.Application.Services;

namespace Cuadre.Api.Endpoints.Categories;

public static class DeleteCategoryEndpoint
{
    private const string Name = "DeleteCategory";

    public static IEndpointRouteBuilder MapDeleteCategory(this IEndpointRouteBuilder app)
    {
        app.MapDelete(ApiEndpoints.Categories.Delete, async (
                Guid id,
                ICategoryService categoryService,
                CancellationToken token) =>
            {
                var result = await categoryService.DeleteAsync(id, token);
                return result.ToNoContentResult();
            })
            .WithName(Name)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
