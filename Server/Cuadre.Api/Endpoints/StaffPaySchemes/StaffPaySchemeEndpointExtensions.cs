namespace Cuadre.Api.Endpoints.StaffPaySchemes;

/// <summary>Configuración › Personal: how the post is paid.</summary>
public static class StaffPaySchemeEndpointExtensions
{
    public static IEndpointRouteBuilder MapStaffPaySchemeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateStaffPayScheme();
        app.MapGetAllStaffPaySchemes();
        return app;
    }
}
