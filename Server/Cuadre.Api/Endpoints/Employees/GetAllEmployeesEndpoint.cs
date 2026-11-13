using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Employees;

public static class GetAllEmployeesEndpoint
{
    private const string Name = "GetAllEmployees";

    public static IEndpointRouteBuilder MapGetAllEmployees(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Employees.GetAll, async (
                IStaffService staffService,
                CancellationToken token) =>
            {
                var employees = await staffService.GetEmployeesAsync(token);
                return TypedResults.Ok(employees.MapToResponse());
            })
            .WithName(Name)
            .Produces<EmployeesResponse>();

        return app;
    }
}
