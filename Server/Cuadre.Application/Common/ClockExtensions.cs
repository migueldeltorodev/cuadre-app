namespace Cuadre.Application.Common;

public static class ClockExtensions
{
    /// <summary>Audit timestamp: an instant, always UTC.</summary>
    public static DateTime UtcNow(this TimeProvider clock) => clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Business date: the calendar day in the PC's local time zone (Cuba).
    /// A close at 21:00 must belong to "today", never to tomorrow's UTC date.
    /// </summary>
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
