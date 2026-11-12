using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.PaymentMethods;

public static class GetAllPaymentMethodsEndpoint
{
    private const string Name = "GetAllPaymentMethods";

    public static IEndpointRouteBuilder MapGetAllPaymentMethods(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.PaymentMethods.GetAll, async (
                IPaymentMethodService paymentMethodService,
                CancellationToken token) =>
            {
                var paymentMethods = await paymentMethodService.GetAllAsync(token);
                return TypedResults.Ok(paymentMethods.MapToResponse());
            })
            .WithName(Name)
            .Produces<PaymentMethodsResponse>();

        return app;
    }
}
