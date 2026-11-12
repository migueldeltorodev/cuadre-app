using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Categories;

public static class UpdateCategoryEndpoint
{
    private const string Name = "UpdateCategory";

    public static IEndpointRouteBuilder MapUpdateCategory(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.Categories.Update, async (
                Guid id,
                UpdateCategoryRequest request,
                ICategoryService categoryService,
                CancellationToken token) =>
            {
                var result = await categoryService.UpdateAsync(request.MapToCategoryChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<CategoryResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
