using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class GetDailyClosingEndpoint
{
    public const string Name = "GetDailyClosing";

    public static IEndpointRouteBuilder MapGetDailyClosing(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.DailyClosings.Get, async (
                Guid id,
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var result = await dailyClosingService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<DailyClosingResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
