using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

public class BackupNamingTests
{
    [Fact]
    public void Format_ShouldUseTheLocalTimeAndTheReasonInSpanish()
    {
        BackupNaming.Format(new DateTime(2026, 11, 14, 21, 30, 5), BackupReason.Closing)
            .Should().Be("cuadre-2026-11-14_213005-cierre.zip");
    }

    [Fact]
    public void Format_ShouldNumberTheSecondBackupOfTheSameSecond()
    {
        BackupNaming.Format(new DateTime(2026, 11, 14, 21, 30, 5), BackupReason.Manual, 2)
            .Should().Be("cuadre-2026-11-14_213005-manual-2.zip");
    }

    [Theory]
    [InlineData(BackupReason.Closing, 1)]
    [InlineData(BackupReason.AppExit, 1)]
    [InlineData(BackupReason.Manual, 3)]
    [InlineData(BackupReason.BeforeUpdate, 1)]
    [InlineData(BackupReason.BeforeRestore, 12)]
    public void TryParse_ShouldReadBackWhatFormatWrote(BackupReason reason, int sequence)
    {
        var local = new DateTime(2026, 1, 2, 3, 4, 5);

        var parsed = BackupNaming.TryParse(BackupNaming.Format(local, reason, sequence), out var at, out var why);

        parsed.Should().BeTrue();
        at.Should().Be(local);
        why.Should().Be(reason);
    }

    [Theory]
    [InlineData("cuadre-2026-11-14_213005-cierre.zip", 1)]
    [InlineData("cuadre-2026-11-14_213005-antes-de-restaurar-2.zip", 2)]
    [InlineData("cuadre-2026-11-14_213005-manual-10.zip", 10)]
    public void SequenceOf_ShouldReadTheNumberOfTheSameSecond(string fileName, int expected)
    {
        BackupNaming.SequenceOf(fileName).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"..\..\data\cuadre.db")]
    [InlineData(@"..\cuadre-2026-11-14_213005-cierre.zip")]
    [InlineData("cuadre-2026-11-14_213005-otra-cosa.zip")]
    [InlineData("cuadre-2026-13-40_213005-cierre.zip")]
    [InlineData("cuadre-2026-11-14_213005-cierre.zip.tmp")]
    [InlineData("CUADRE-2026-11-14_213005-cierre.zip")]
    public void TryParse_ShouldRejectAnythingElse_AlsoPathsOutsideTheFolder(string fileName)
    {
        BackupNaming.TryParse(fileName, out _, out _).Should().BeFalse();
    }
}

public class BackupRotationTests
{
    private static BackupEntry Entry(DateTime local, BackupReason reason = BackupReason.Closing) =>
        new(BackupNaming.Format(local, reason), BackupLocation.Local, local, local, reason, 1_000);

    private static DateTime Day(int month, int day, int hour = 21) => new(2026, month, day, hour, 0, 0);

    [Fact]
    public void SelectToDelete_ShouldKeepEverything_WhileThereAreFewBackups()
    {
        var entries = Enumerable.Range(1, 5).Select(day => Entry(Day(11, day))).ToList();

        BackupRotation.SelectToDelete(entries).Should().BeEmpty();
    }

    [Fact]
    public void SelectToDelete_ShouldKeepTheLast7Days_PlusTheNewestOfEachWeekAndMonth()
    {
        // 1 to 10 November 2026. Sunday the 1st closes the ISO week of 26 October; 2-8 and 9-10 are the next ones.
        // Daily: 4..10. Weekly: the 10th, the 8th and the 1st. Monthly: the 10th. Out: the 2nd and the 3rd.
        var entries = Enumerable.Range(1, 10).Select(day => Entry(Day(11, day))).ToList();

        BackupRotation.SelectToDelete(entries).Select(e => e.CreatedAtLocal)
            .Should().Equal(Day(11, 3), Day(11, 2));
    }

    [Fact]
    public void SelectToDelete_ShouldKeepOnlyTheNewestOfTheSameDay()
    {
        var morning = Entry(Day(11, 10, 8), BackupReason.Manual);
        var closing = Entry(Day(11, 10, 21));
        var exit = Entry(Day(11, 10, 22), BackupReason.AppExit);

        BackupRotation.SelectToDelete([morning, closing, exit]).Should().Equal(closing, morning);
    }

    [Fact]
    public void SelectToDelete_ShouldKeepTheLastOfTheSameSecond_ByItsNumberNotItsText()
    {
        // As text "manual.zip" sorts after "manual-2.zip": the newest one would be the one deleted.
        var at = Day(11, 10);
        var first = Entry(at, BackupReason.Manual);
        var second = first with { FileName = BackupNaming.Format(at, BackupReason.Manual, 2) };
        var tenth = first with { FileName = BackupNaming.Format(at, BackupReason.Manual, 10) };

        BackupRotation.SelectToDelete([first, tenth, second]).Should().Equal(second, first);
    }

    [Fact]
    public void SelectToDelete_ShouldCountDaysThatHaveABackup_NotCalendarDays()
    {
        // Closed for a month in the middle: the 7 daily copies still reach back across the gap.
        var entries = new[] { Day(9, 26), Day(9, 27), Day(9, 28), Day(9, 29), Day(10, 30), Day(10, 31), Day(11, 1) }
            .Select(day => Entry(day)).ToList();

        BackupRotation.SelectToDelete(entries).Should().BeEmpty();
    }

    [Fact]
    public void SelectToDelete_ShouldKeep12Months()
    {
        // One backup on the 1st of each month for 14 months: only the 2 oldest go.
        var entries = Enumerable.Range(0, 14).Select(i => Entry(new DateTime(2025, 10, 1, 21, 0, 0).AddMonths(i)))
            .ToList();

        BackupRotation.SelectToDelete(entries).Select(e => e.CreatedAtLocal)
            .Should().Equal(new DateTime(2025, 11, 1, 21, 0, 0), new DateTime(2025, 10, 1, 21, 0, 0));
    }

    [Fact]
    public void SelectToDelete_ShouldKeepTheLast5SafetyCopies_WithoutPushingOutTheDailyOnes()
    {
        var daily = Enumerable.Range(4, 7).Select(day => Entry(Day(11, day))).ToList();
        var safety = Enumerable.Range(4, 7).Select(day => Entry(Day(11, day, 23), BackupReason.BeforeRestore))
            .ToList();

        BackupRotation.SelectToDelete([.. daily, .. safety]).Select(e => e.CreatedAtLocal)
            .Should().Equal(Day(11, 5, 23), Day(11, 4, 23));
    }
}

public class BackupAlertsTests
{
    private static readonly DateOnly Today = new(2026, 11, 14);

    private static BackupState State(string? folder = @"E:\Cuadre respaldos", DateTime? lastExternalUtc = null,
        BackupProblem? problem = null) => new()
    {
        Settings = new BackupSettings { ExternalDirectory = folder, AlertAfterDays = 7 },
        LastExternal = lastExternalUtc is { } at ? new BackupMark(at, "x.zip") : null,
        LastProblem = problem
    };

    [Fact]
    public void Evaluate_ShouldAskForAPendrive_WhenNoneWasChosen()
    {
        BackupAlerts.Evaluate(State(folder: null), Today, TimeZoneInfo.Utc)
            .Should().Be(new BackupAlert(BackupAlertKind.NotConfigured, null));
    }

    [Fact]
    public void Evaluate_ShouldSayNothingWasCopiedYet_WhenTheFolderIsNew()
    {
        BackupAlerts.Evaluate(State(), Today, TimeZoneInfo.Utc)
            .Should().Be(new BackupAlert(BackupAlertKind.NeverCopied, null));
    }

    [Fact]
    public void Evaluate_ShouldBeQuiet_UpToTheDaysChosen()
    {
        BackupAlerts.Evaluate(State(lastExternalUtc: new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc)), Today,
            TimeZoneInfo.Utc).Should().BeNull();
    }

    [Fact]
    public void Evaluate_ShouldWarn_WhenTheLastPendriveCopyIsOlder()
    {
        BackupAlerts.Evaluate(State(lastExternalUtc: new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc)), Today,
            TimeZoneInfo.Utc).Should().Be(new BackupAlert(BackupAlertKind.Overdue, 8));
    }

    [Fact]
    public void Evaluate_ShouldCountDaysInTheShopsTime()
    {
        // 03:00 UTC on the 7th is still the 6th in Cuba (UTC-5): 8 days, not 7.
        var cuba = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");

        BackupAlerts.Evaluate(State(lastExternalUtc: new DateTime(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc)), Today, cuba)
            .Should().Be(new BackupAlert(BackupAlertKind.Overdue, 8));
    }

    [Fact]
    public void Evaluate_ShouldPutAFailureFirst()
    {
        var problem = new BackupProblem(new DateTime(2026, 11, 14, 1, 0, 0, DateTimeKind.Utc), "Sin espacio");

        BackupAlerts.Evaluate(State(lastExternalUtc: new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc),
                problem: problem), Today, TimeZoneInfo.Utc)
            .Should().Be(new BackupAlert(BackupAlertKind.Failed, 1));
    }
}
