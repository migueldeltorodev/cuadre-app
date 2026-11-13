using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.EnvelopeOperations;

public static class GetAllEnvelopeOperationsEndpoint
{
    private const string Name = "GetAllEnvelopeOperations";

    public static IEndpointRouteBuilder MapGetAllEnvelopeOperations(this IEndpointRouteBuilder app)
    {
        // ?kind=Withdrawal&from=...&to=...&envelopeId=...&includeVoided=true
        app.MapGet(ApiEndpoints.EnvelopeOperations.GetAll, async (
                [AsParameters] GetAllEnvelopeOperationsRequest request,
                IEnvelopeOperationService envelopeOperationService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var operations = await envelopeOperationService.GetAllAsync(options, token);
                var total = await envelopeOperationService.GetCountAsync(options, token);
                return TypedResults.Ok(operations.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<EnvelopeOperationsResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
