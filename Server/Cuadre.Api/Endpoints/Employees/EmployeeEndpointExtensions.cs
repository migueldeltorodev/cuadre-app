namespace Cuadre.Api.Endpoints.Employees;

/// <summary>Configuración › Personal: who works the counter.</summary>
public static class EmployeeEndpointExtensions
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateEmployee();
        app.MapGetAllEmployees();
        app.MapUpdateEmployee();
        return app;
    }
}
