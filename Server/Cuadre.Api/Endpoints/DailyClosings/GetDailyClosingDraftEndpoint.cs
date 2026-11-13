using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class GetDailyClosingDraftEndpoint
{
    private const string Name = "GetDailyClosingDraft";

    public static IEndpointRouteBuilder MapGetDailyClosingDraft(this IEndpointRouteBuilder app)
    {
        // The "planilla" and everything the closing screens need. ?date=2026-11-12 (default: today).
        app.MapGet(ApiEndpoints.DailyClosings.Draft, async (
                [AsParameters] GetDailyClosingDraftRequest request,
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var draft = await dailyClosingService.GetDraftAsync(request.Date, token);
                return TypedResults.Ok(draft.MapToResponse());
            })
            .WithName(Name)
            .Produces<DailyClosingDraftResponse>();

        return app;
    }
}
