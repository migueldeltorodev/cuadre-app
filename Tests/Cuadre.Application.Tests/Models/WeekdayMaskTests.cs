using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

public class WeekdayMaskTests
{
    [Fact]
    public void From_ShouldSetOneBitPerDay_WithSundayAsBitZero()
    {
        WeekdayMask.From([DayOfWeek.Sunday]).Should().Be(1);
        WeekdayMask.From([DayOfWeek.Saturday]).Should().Be(64);
        WeekdayMask.From([DayOfWeek.Sunday, DayOfWeek.Monday]).Should().Be(3);
    }

    [Fact]
    public void From_ShouldIgnoreRepeatedDays()
    {
        WeekdayMask.From([DayOfWeek.Friday, DayOfWeek.Friday]).Should().Be(WeekdayMask.From([DayOfWeek.Friday]));
    }

    [Fact]
    public void ToDays_ShouldRoundTrip_EveryCombination()
    {
        for (var mask = 0; mask <= WeekdayMask.AllDays; mask++)
        {
            WeekdayMask.From(WeekdayMask.ToDays(mask)).Should().Be(mask);
        }
    }
}
