namespace Cuadre.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Deterministic clock: "today" is whatever the test says, so date rules ("not in the future",
/// "no rate loaded today") are testable. Local time zone = UTC to keep the arithmetic obvious.
/// Reset to <see cref="Default"/> before every test.
/// </summary>
public sealed class TestClock : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; set; } = Default;

    public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
