using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.PaymentMethods;

public static class GetPaymentMethodEndpoint
{
    public const string Name = "GetPaymentMethod";

    public static IEndpointRouteBuilder MapGetPaymentMethod(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.PaymentMethods.Get, async (
                Guid id,
                IPaymentMethodService paymentMethodService,
                CancellationToken token) =>
            {
                var result = await paymentMethodService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<PaymentMethodResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
