namespace Cuadre.Api.Endpoints.Audit;

/// <summary>RF-DAT-08: the audit log (read-only).</summary>
public static class AuditEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAuditEvents();
        return app;
    }
}
