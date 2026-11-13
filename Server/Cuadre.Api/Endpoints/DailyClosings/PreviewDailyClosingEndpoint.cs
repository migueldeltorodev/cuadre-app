using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.DailyClosings;

public static class PreviewDailyClosingEndpoint
{
    private const string Name = "PreviewDailyClosing";

    public static IEndpointRouteBuilder MapPreviewDailyClosing(this IEndpointRouteBuilder app)
    {
        // Computes the whole closing WITHOUT saving it: the summary step of the wizard.
        app.MapPost(ApiEndpoints.DailyClosings.Preview, async (
                CreateDailyClosingRequest request,
                IDailyClosingService dailyClosingService,
                CancellationToken token) =>
            {
                var result = await dailyClosingService.PreviewAsync(request.MapToDailyClosingInput(), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<DailyClosingResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
