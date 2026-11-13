using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class ReopenDailyClosingEndpoint
{
    private const string Name = "ReopenDailyClosing";

    public static IEndpointRouteBuilder MapReopenDailyClosing(this IEndpointRouteBuilder app)
    {
        // POST, not DELETE: the closing stays, marked as voided, with its reversal movements dated the same day.
        app.MapPost(ApiEndpoints.DailyClosings.Reopen, async (
                Guid id,
                ReopenDailyClosingRequest request,
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var result = await dailyClosingService.ReopenAsync(request.MapToReopenDailyClosing(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<DailyClosingResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
