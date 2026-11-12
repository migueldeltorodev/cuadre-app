using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.PaymentMethods;

public static class CreatePaymentMethodEndpoint
{
    private const string Name = "CreatePaymentMethod";

    public static IEndpointRouteBuilder MapCreatePaymentMethod(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.PaymentMethods.Create, async (
                CreatePaymentMethodRequest request,
                IPaymentMethodService paymentMethodService,
                CancellationToken token) =>
            {
                var result = await paymentMethodService.CreateAsync(request.MapToPaymentMethod(), token);

                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetPaymentMethodEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<PaymentMethodResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
