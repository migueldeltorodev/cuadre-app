namespace Cuadre.Api.Endpoints.Categories;

/// <summary>Two levels (category → subcategory). DELETE only while unused (422 otherwise).</summary>
public static class CategoryEndpointExtensions
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateCategory();
        app.MapGetCategory();
        app.MapGetAllCategories();
        app.MapUpdateCategory();
        app.MapDeleteCategory();
        return app;
    }
}
