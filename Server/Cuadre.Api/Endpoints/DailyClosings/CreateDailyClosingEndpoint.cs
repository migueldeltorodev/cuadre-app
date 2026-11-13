using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class CreateDailyClosingEndpoint
{
    private const string Name = "CreateDailyClosing";

    public static IEndpointRouteBuilder MapCreateDailyClosing(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.DailyClosings.Create, async (
                CreateDailyClosingRequest request,
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var result = await dailyClosingService.CreateAsync(request.MapToDailyClosingInput(), token);
                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetDailyClosingEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<DailyClosingResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
