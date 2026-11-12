namespace Cuadre.Api.Endpoints.PaymentMethods;

/// <summary>No DELETE: payment methods are deactivated (PUT isActive=false), never removed.</summary>
public static class PaymentMethodEndpointExtensions
{
    public static IEndpointRouteBuilder MapPaymentMethodEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreatePaymentMethod();
        app.MapGetPaymentMethod();
        app.MapGetAllPaymentMethods();
        app.MapUpdatePaymentMethod();
        return app;
    }
}
