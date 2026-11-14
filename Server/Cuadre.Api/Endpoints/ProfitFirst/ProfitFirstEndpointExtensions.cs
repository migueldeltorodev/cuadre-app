namespace Cuadre.Api.Endpoints.ProfitFirst;

/// <summary>RF-PF-10: the evaluation of a period.</summary>
public static class ProfitFirstEndpointExtensions
{
    public static IEndpointRouteBuilder MapProfitFirstEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetProfitFirstEvaluation();
        return app;
    }
}
