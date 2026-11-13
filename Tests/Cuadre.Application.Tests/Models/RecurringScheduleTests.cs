using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// RF-GAS-01. 2026-11-12 is a Thursday; 2026-11-09 a Monday.

public class RecurringScheduleTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly HashSet<DateOnly> NothingTaken = [];

    [Fact]
    public void Monthly_ShouldBeDueOnItsDay_FromTheStartToToday()
    {
        var due = RecurringSchedule.Due(RecurringFrequency.Monthly, 10, new DateOnly(2026, 9, 15), Today, NothingTaken);

        // 10/9 is before the start; 10/10 and 10/11 are due.
        due.Should().Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)),
            (new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10)));
    }

    [Fact]
    public void Monthly_ShouldNotBeDue_BeforeItsDayComes()
    {
        RecurringSchedule.Due(RecurringFrequency.Monthly, 20, new DateOnly(2026, 11, 1), Today, NothingTaken)
            .Should().BeEmpty();
    }

    [Fact]
    public void Monthly_ShouldBeDue_OnTheVeryDay()
    {
        RecurringSchedule.Due(RecurringFrequency.Monthly, 12, new DateOnly(2026, 11, 1), Today, NothingTaken)
            .Should().Equal((new DateOnly(2026, 11, 1), Today));
    }

    [Fact]
    public void Weekly_ShouldBeDueOnItsWeekday_WithTheMondayAsThePeriod()
    {
        var due = RecurringSchedule.Due(RecurringFrequency.Weekly, (int)DayOfWeek.Saturday, new DateOnly(2026, 10, 27),
            Today, NothingTaken);

        // Saturdays 31/10 and 7/11 (14/11 has not come yet).
        due.Should().Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 31)),
            (new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 7)));
    }

    [Fact]
    public void Weekly_ShouldPutSundayAtTheEndOfItsWeek()
    {
        var due = RecurringSchedule.Due(RecurringFrequency.Weekly, (int)DayOfWeek.Sunday, new DateOnly(2026, 11, 2),
            Today, NothingTaken);

        due.Should().Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)));
    }

    [Fact]
    public void Due_ShouldLeaveOutTheTakenPeriods()
    {
        var due = RecurringSchedule.Due(RecurringFrequency.Monthly, 1, new DateOnly(2026, 9, 1), Today,
            new HashSet<DateOnly> { new(2026, 10, 1) });

        due.Select(x => x.PeriodStart).Should().Equal(new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 1));
    }

    [Fact]
    public void Due_ShouldListOnlyTheLatestOnes_WhenTheTemplateIsOld()
    {
        var due = RecurringSchedule.Due(RecurringFrequency.Monthly, 1, new DateOnly(2020, 1, 1), Today, NothingTaken);

        due.Should().HaveCount(RecurringSchedule.MaxListed);
        due[^1].PeriodStart.Should().Be(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public void Due_ShouldBeEmpty_WhenItStartsInTheFuture()
    {
        RecurringSchedule.Due(RecurringFrequency.Weekly, 1, Today.AddDays(1), Today, NothingTaken).Should().BeEmpty();
    }
}
