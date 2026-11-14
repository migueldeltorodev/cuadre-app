using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class GetPendingClosingDaysEndpoint
{
    private const string Name = "GetPendingClosingDays";

    public static IEndpointRouteBuilder MapGetPendingClosingDays(this IEndpointRouteBuilder app)
    {
        // RF-CIE-09: the business days already over (up to yesterday) that are still not closed.
        app.MapGet(ApiEndpoints.DailyClosings.Pending, async (
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var pending = await dailyClosingService.GetPendingDaysAsync(token);
                return TypedResults.Ok(pending.MapToResponse());
            })
            .WithName(Name)
            .Produces<PendingClosingDaysResponse>();

        return app;
    }
}
