using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StaffPaySchemes;

public static class CreateStaffPaySchemeEndpoint
{
    private const string Name = "CreateStaffPayScheme";

    public static IEndpointRouteBuilder MapCreateStaffPayScheme(this IEndpointRouteBuilder app)
    {
        // Same start date as the latest scheme = it is replaced; an earlier or closed date is refused (422).
        app.MapPost(ApiEndpoints.StaffPaySchemes.Create, async (
                CreateStaffPaySchemeRequest request,
                IStaffService staffService,
                CancellationToken token) =>
            {
                var result = await staffService.SavePaySchemeAsync(request.MapToStaffPayScheme(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.StaffPaySchemes.GetAll}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<StaffPaySchemeResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
