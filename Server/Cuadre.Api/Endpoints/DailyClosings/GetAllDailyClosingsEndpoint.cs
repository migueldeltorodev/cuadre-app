using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class GetAllDailyClosingsEndpoint
{
    private const string Name = "GetAllDailyClosings";

    public static IEndpointRouteBuilder MapGetAllDailyClosings(this IEndpointRouteBuilder app)
    {
        // ?from=...&to=...&includeVoided=true. Newest first.
        app.MapGet(ApiEndpoints.DailyClosings.GetAll, async (
                [AsParameters] GetAllDailyClosingsRequest request,
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var closings = await dailyClosingService.GetAllAsync(options, token);
                var total = await dailyClosingService.GetCountAsync(options, token);
                return TypedResults.Ok(closings.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<DailyClosingsResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
