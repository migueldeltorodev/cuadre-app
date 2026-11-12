using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Envelopes;

public static class GetAllEnvelopesEndpoint
{
    private const string Name = "GetAllEnvelopes";

    public static IEndpointRouteBuilder MapGetAllEnvelopes(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Envelopes.GetAll, async (
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var envelopes = await envelopeService.GetAllAsync(token);
                return TypedResults.Ok(envelopes.MapToResponse());
            })
            .WithName(Name)
            .Produces<EnvelopesResponse>();

        return app;
    }
}
