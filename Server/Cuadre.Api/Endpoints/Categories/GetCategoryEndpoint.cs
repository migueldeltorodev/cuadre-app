using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Categories;

public static class GetCategoryEndpoint
{
    public const string Name = "GetCategory";

    public static IEndpointRouteBuilder MapGetCategory(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Categories.Get, async (
                Guid id,
                ICategoryService categoryService,
                CancellationToken token) =>
            {
                var result = await categoryService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<CategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
