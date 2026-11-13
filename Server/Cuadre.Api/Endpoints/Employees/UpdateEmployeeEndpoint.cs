using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Employees;

public static class UpdateEmployeeEndpoint
{
    private const string Name = "UpdateEmployee";

    public static IEndpointRouteBuilder MapUpdateEmployee(this IEndpointRouteBuilder app)
    {
        // Rename or (de)activate.
        app.MapPut(ApiEndpoints.Employees.Update, async (
                Guid id,
                UpdateEmployeeRequest request,
                IStaffService staffService,
                CancellationToken token) =>
            {
                var result = await staffService.UpdateEmployeeAsync(request.MapToEmployeeChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<EmployeeResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
