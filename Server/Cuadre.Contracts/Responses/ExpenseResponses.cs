using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

public class ExpenseCategoryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>Confirmed expenses in it.</summary>
    public required int ExpenseCount { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class ExpenseCategoriesResponse
{
    /// <summary>Active first, then by name.</summary>
    public required IEnumerable<ExpenseCategoryResponse> Items { get; init; } = [];
}

public class ExpenseResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string Description { get; init; }

    /// <summary>In the currency of the cash box.</summary>
    public required long AmountCents { get; init; }

    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }
    public string? Notes { get; init; }

    /// <summary>A confirmed occurrence of a recurring expense.</summary>
    public Guid? RecurringExpenseId { get; init; }

    public string? RecurringExpenseName { get; init; }
    public DateOnly? PeriodStart { get; init; }

    public required ExpenseState Status { get; init; }
    public string? VoidReason { get; init; }
    public DateTime? VoidedAt { get; init; }

    /// <summary>Only right after creating it: the envelope it left below zero (allowed, RF-PF-08).</summary>
    public NegativeEnvelopeWarningResponse? Warning { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class ExpenseTotalResponse
{
    public required CurrencyCode Currency { get; init; }
    public required long AmountCents { get; init; }
}

public class ExpensesResponse : PagedResponse<ExpenseResponse>
{
    /// <summary>The confirmed expenses of the whole filter (not only this page), per currency.</summary>
    public required IReadOnlyList<ExpenseTotalResponse> Totals { get; init; } = [];
}

public class RecurringExpenseResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required long AmountCents { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }
    public required ExpenseFrequency Frequency { get; init; }
    public required int DueDay { get; init; }
    public required DateOnly StartsOn { get; init; }
    public string? Notes { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class RecurringExpensesResponse
{
    /// <summary>Active first, then by name.</summary>
    public required IEnumerable<RecurringExpenseResponse> Items { get; init; } = [];
}

/// <summary>An occurrence waiting to be confirmed or skipped.</summary>
public class DueExpenseResponse
{
    public required RecurringExpenseResponse Recurring { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly DueDate { get; init; }
}

public class DueExpensesResponse
{
    /// <summary>Oldest first.</summary>
    public required IEnumerable<DueExpenseResponse> Items { get; init; } = [];
}
