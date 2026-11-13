using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Employees;

public static class CreateEmployeeEndpoint
{
    private const string Name = "CreateEmployee";

    public static IEndpointRouteBuilder MapCreateEmployee(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Employees.Create, async (
                CreateEmployeeRequest request,
                IStaffService staffService,
                CancellationToken token) =>
            {
                var result = await staffService.CreateEmployeeAsync(request.MapToEmployee(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.Employees.GetAll}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<EmployeeResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
