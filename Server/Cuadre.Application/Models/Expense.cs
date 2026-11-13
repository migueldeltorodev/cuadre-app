namespace Cuadre.Application.Models;

/// <summary>RF-GAS-03: Gestor, Transporte, Servicios... Never deleted (expenses point to it), only deactivated.</summary>
public class ExpenseCategory
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Read-only, filled by queries: confirmed expenses in it.</summary>
    public int ExpenseCount { get; init; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record ExpenseCategoryChanges(Guid Id, string Name, bool IsActive);

/// <summary>
/// A gasto (RF-GAS-02): money out of a cash box and an envelope. Operación by default; never Reposición, which only
/// pays merchandise (D-01). Saving it confirms it; never edited, only voided with a reason (RF-GAS-04).
/// </summary>
public class Expense
{
    public required Guid Id { get; init; }
    public int Number { get; set; }
    public required DateOnly Date { get; init; }
    public required Guid CategoryId { get; init; }
    public required string Description { get; init; }

    /// <summary>Positive, in the currency of the cash box.</summary>
    public required long AmountCents { get; init; }

    public required Guid CashBoxId { get; init; }

    /// <summary>Null on input = Operación (resolved by the service).</summary>
    public Guid? EnvelopeId { get; set; }

    public string? Notes { get; init; }

    /// <summary>A confirmed occurrence of a recurring expense, and the first day of its period.</summary>
    public Guid? RecurringExpenseId { get; init; }

    public DateOnly? PeriodStart { get; init; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Confirmed;
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Read-only, filled by queries.</summary>
    public string CategoryName { get; init; } = "";

    public string CashBoxName { get; init; } = "";
    public Currency Currency { get; init; }
    public string EnvelopeName { get; init; } = "";
    public string? RecurringExpenseName { get; init; }
}

public enum ExpenseStatus
{
    Confirmed,
    Voided
}

public class GetAllExpensesOptions
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? CashBoxId { get; init; }
    public Guid? EnvelopeId { get; init; }
    public Guid? RecurringExpenseId { get; init; }
    public bool IncludeVoided { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>What the confirmed expenses of a filter add up to, per currency (currencies are never added up).</summary>
public sealed record ExpenseTotal(Currency Currency, long AmountCents);

public sealed record VoidExpense(Guid ExpenseId, string Reason);

/// <summary>The expense saved, and the envelope it left below zero, if it did (allowed, RF-PF-08).</summary>
public sealed record ExpenseResult(Expense Expense, NegativeEnvelopeWarning? Warning);

/// <summary>
/// RF-GAS-01: the gestor every month, the transport every week... The app proposes each occurrence from StartsOn
/// on, and the owner confirms it (an expense, the amount can change) or skips it. Editing it never touches the
/// expenses already confirmed.
/// </summary>
public class RecurringExpense
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required Guid CategoryId { get; set; }
    public required long AmountCents { get; set; }
    public required Guid CashBoxId { get; set; }

    /// <summary>Null on input = Operación (resolved by the service).</summary>
    public Guid? EnvelopeId { get; set; }

    public required RecurringFrequency Frequency { get; set; }

    /// <summary>Monthly: day of the month, 1 to 28. Weekly: <see cref="DayOfWeek"/> (0 = Sunday).</summary>
    public required int DueDay { get; set; }

    public required DateOnly StartsOn { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Read-only, filled by queries.</summary>
    public string CategoryName { get; init; } = "";

    public string CashBoxName { get; init; } = "";
    public Currency Currency { get; init; }
    public string EnvelopeName { get; init; } = "";
}

/// <summary>Stored by name: append only.</summary>
public enum RecurringFrequency
{
    Monthly,
    Weekly
}

/// <summary>An occurrence of a recurring expense not confirmed nor skipped yet.</summary>
public sealed record DueExpense(RecurringExpense Recurring, DateOnly PeriodStart, DateOnly DueDate);

/// <summary>
/// Confirms one occurrence: an expense with the template's data, except what the owner changes here.
/// Date null = today (the day the money really left).
/// </summary>
public sealed record ConfirmRecurringExpense(
    Guid RecurringExpenseId,
    DateOnly PeriodStart,
    DateOnly? Date,
    long? AmountCents,
    Guid? CashBoxId,
    string? Notes);

public sealed record SkipRecurringExpense(Guid RecurringExpenseId, DateOnly PeriodStart);

/// <summary>When each occurrence of a recurring expense falls (pure: unit-tested).</summary>
public static class RecurringSchedule
{
    /// <summary>The most a list of due occurrences shows: an old forgotten template must not flood the screen.</summary>
    public const int MaxListed = 12;

    public const int MaxDayOfMonth = 28;

    /// <summary>
    /// The occurrences already due (due date between StartsOn and today) whose period is not taken (confirmed or
    /// skipped). Oldest first, the latest <see cref="MaxListed"/> only.
    /// </summary>
    public static IReadOnlyList<(DateOnly PeriodStart, DateOnly DueDate)> Due(RecurringFrequency frequency,
        int dueDay, DateOnly startsOn, DateOnly today, IReadOnlySet<DateOnly> taken)
    {
        var due = new List<(DateOnly, DateOnly)>();
        if (startsOn > today)
        {
            return due;
        }

        var period = PeriodStart(frequency, startsOn);
        var last = PeriodStart(frequency, today);
        while (period <= last)
        {
            var dueDate = DueDate(frequency, dueDay, period);
            if (dueDate >= startsOn && dueDate <= today && !taken.Contains(period))
            {
                due.Add((period, dueDate));
            }

            period = frequency == RecurringFrequency.Monthly ? period.AddMonths(1) : period.AddDays(7);
        }

        return due.TakeLast(MaxListed).ToList();
    }

    /// <summary>Monthly: the 1st of the month. Weekly: the Monday of the week.</summary>
    public static DateOnly PeriodStart(RecurringFrequency frequency, DateOnly date) => frequency switch
    {
        RecurringFrequency.Monthly => new DateOnly(date.Year, date.Month, 1),
        RecurringFrequency.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
    };

    public static DateOnly DueDate(RecurringFrequency frequency, int dueDay, DateOnly periodStart) => frequency switch
    {
        RecurringFrequency.Monthly => periodStart.AddDays(dueDay - 1),
        RecurringFrequency.Weekly => periodStart.AddDays((dueDay + 6) % 7),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
    };
}
