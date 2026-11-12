namespace Cuadre.Application.Models;

/// <summary>
/// Packs a set of weekdays into one INTEGER column: bit n set = (DayOfWeek)n.
/// Sunday = 1, Monday = 2, ... Saturday = 64; all seven = 127.
/// </summary>
public static class WeekdayMask
{
    public const int AllDays = 0b111_1111;

    public static int From(IEnumerable<DayOfWeek> days) =>
        days.Aggregate(0, (mask, day) => mask | (1 << (int)day));

    public static IReadOnlyCollection<DayOfWeek> ToDays(int mask) =>
        Enum.GetValues<DayOfWeek>().Where(day => (mask & (1 << (int)day)) != 0).ToArray();
}
