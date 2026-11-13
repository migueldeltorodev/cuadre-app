using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// RF-CIE-09. 2026-11-12 is a Thursday; 2026-11-08 a Sunday.

public class ClosingCalendarTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public void PendingDays_ShouldGoFromTheDayAfterTheLastClosingToYesterday()
    {
        var days = ClosingCalendar.PendingDays(new DateOnly(2026, 11, 9), null, Today, []);

        days.Should().Equal(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11));
    }

    [Fact]
    public void PendingDays_ShouldSkipTheDaysTheBusinessIsClosed()
    {
        var days = ClosingCalendar.PendingDays(new DateOnly(2026, 11, 6), null, Today, [DayOfWeek.Sunday]);

        // 7 (Sat), 8 (Sun: closed), 9, 10, 11
        days.Should().Equal(new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 11));
    }

    [Fact]
    public void PendingDays_ShouldStartAtTheFirstActivity_WhenNothingWasClosedYet()
    {
        var days = ClosingCalendar.PendingDays(null, new DateOnly(2026, 11, 10), Today, []);

        days.Should().Equal(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11));
    }

    [Fact]
    public void PendingDays_ShouldIgnoreTheFirstActivity_WhenThereIsAClosing()
    {
        var days = ClosingCalendar.PendingDays(new DateOnly(2026, 11, 10), new DateOnly(2026, 1, 1), Today, []);

        days.Should().Equal(new DateOnly(2026, 11, 11));
    }

    [Fact]
    public void PendingDays_ShouldBeEmpty_WhenYesterdayOrTodayIsClosed()
    {
        ClosingCalendar.PendingDays(Today.AddDays(-1), null, Today, []).Should().BeEmpty();
        ClosingCalendar.PendingDays(Today, null, Today, []).Should().BeEmpty();
    }

    [Fact]
    public void PendingDays_ShouldNotCountToday_WhenTheActivityStartedToday()
    {
        ClosingCalendar.PendingDays(null, Today, Today, []).Should().BeEmpty();
    }

    [Fact]
    public void PendingDays_ShouldBeEmpty_WithoutActivityNorClosings()
    {
        ClosingCalendar.PendingDays(null, null, Today, []).Should().BeEmpty();
    }
}
