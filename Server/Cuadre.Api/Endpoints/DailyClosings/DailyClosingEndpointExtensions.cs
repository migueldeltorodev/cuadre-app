namespace Cuadre.Api.Endpoints.DailyClosings;

/// <summary>The daily closing: draft, preview, close, list and reopen.</summary>
public static class DailyClosingEndpointExtensions
{
    public static IEndpointRouteBuilder MapDailyClosingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetDailyClosingDraft();
        app.MapPreviewDailyClosing();
        app.MapCreateDailyClosing();
        app.MapGetDailyClosing();
        app.MapGetAllDailyClosings();
        app.MapReopenDailyClosing();
        app.MapGetPendingClosingDays();
        return app;
    }
}
