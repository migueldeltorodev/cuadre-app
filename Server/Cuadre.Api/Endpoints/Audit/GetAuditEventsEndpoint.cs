using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Audit;

public static class GetAuditEventsEndpoint
{
    private const string Name = "GetAuditEvents";

    public static IEndpointRouteBuilder MapGetAuditEvents(this IEndpointRouteBuilder app)
    {
        // ?from=...&to=...&kind=Voided&page=1&pageSize=25
        app.MapGet(ApiEndpoints.Audit.GetAll, async (
                [AsParameters] GetAuditEventsRequest request,
                IAuditService auditService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var page = await auditService.GetEventsAsync(options, token);
                return TypedResults.Ok(page.MapToResponse(options.Page, options.PageSize));
            })
            .WithName(Name)
            .Produces<PagedResponse<AuditEventResponse>>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
