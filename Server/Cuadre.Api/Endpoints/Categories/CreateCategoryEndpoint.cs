using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Categories;

public static class CreateCategoryEndpoint
{
    private const string Name = "CreateCategory";

    public static IEndpointRouteBuilder MapCreateCategory(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Categories.Create, async (
                CreateCategoryRequest request,
                ICategoryService categoryService,
                CancellationToken token) =>
            {
                var result = await categoryService.CreateAsync(request.MapToCategory(), token);

                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetCategoryEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<CategoryResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
