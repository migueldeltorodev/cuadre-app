using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.PaymentMethods;

public static class UpdatePaymentMethodEndpoint
{
    private const string Name = "UpdatePaymentMethod";

    public static IEndpointRouteBuilder MapUpdatePaymentMethod(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.PaymentMethods.Update, async (
                Guid id,
                UpdatePaymentMethodRequest request,
                IPaymentMethodService paymentMethodService,
                CancellationToken token) =>
            {
                var result = await paymentMethodService.UpdateAsync(request.MapToPaymentMethod(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<PaymentMethodResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
