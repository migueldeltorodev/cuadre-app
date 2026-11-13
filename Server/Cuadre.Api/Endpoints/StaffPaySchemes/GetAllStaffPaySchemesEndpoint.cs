using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StaffPaySchemes;

public static class GetAllStaffPaySchemesEndpoint
{
    private const string Name = "GetAllStaffPaySchemes";

    public static IEndpointRouteBuilder MapGetAllStaffPaySchemes(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.StaffPaySchemes.GetAll, async (
                IStaffService staffService,
                CancellationToken token) =>
            {
                var schemes = await staffService.GetPaySchemesAsync(token);
                return TypedResults.Ok(schemes.MapToResponse());
            })
            .WithName(Name)
            .Produces<StaffPaySchemesResponse>();

        return app;
    }
}
