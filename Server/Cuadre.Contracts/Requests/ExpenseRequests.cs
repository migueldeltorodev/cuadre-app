using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

public class CreateExpenseCategoryRequest
{
    public required string Name { get; init; }
}

/// <summary>Never deleted, only deactivated: the expenses keep their category.</summary>
public class UpdateExpenseCategoryRequest
{
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>RF-GAS-02. Amount in the currency of the cash box. EnvelopeId null = Gastos de operación.</summary>
public class CreateExpenseRequest
{
    public required DateOnly Date { get; init; }
    public required Guid CategoryId { get; init; }
    public required string Description { get; init; }
    public required long AmountCents { get; init; }
    public required Guid CashBoxId { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? Notes { get; init; }
}

public class VoidExpenseRequest
{
    public required string Reason { get; init; }
}

public class GetAllExpensesRequest : PagedRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? CashBoxId { get; init; }
    public Guid? EnvelopeId { get; init; }
    public Guid? RecurringExpenseId { get; init; }
    public bool? IncludeVoided { get; init; }
}

/// <summary>RF-GAS-01. EnvelopeId null = Gastos de operación.</summary>
public class CreateRecurringExpenseRequest
{
    public required string Name { get; init; }
    public required Guid CategoryId { get; init; }
    public required long AmountCents { get; init; }
    public required Guid CashBoxId { get; init; }
    public Guid? EnvelopeId { get; init; }
    public required ExpenseFrequency Frequency { get; init; }

    /// <summary>Monthly: 1-28. Weekly: 0 (Sunday) to 6 (Saturday).</summary>
    public required int DueDay { get; init; }

    public required DateOnly StartsOn { get; init; }
    public string? Notes { get; init; }
}

public class UpdateRecurringExpenseRequest
{
    public required string Name { get; init; }
    public required Guid CategoryId { get; init; }
    public required long AmountCents { get; init; }
    public required Guid CashBoxId { get; init; }
    public Guid? EnvelopeId { get; init; }
    public required ExpenseFrequency Frequency { get; init; }
    public required int DueDay { get; init; }
    public required DateOnly StartsOn { get; init; }
    public string? Notes { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>
/// Pays one occurrence. Null = what the template says (the amount and the box), today for the date: the day the
/// money really left, not the day it was due.
/// </summary>
public class ConfirmRecurringExpenseRequest
{
    public required DateOnly PeriodStart { get; init; }
    public DateOnly? Date { get; init; }
    public long? AmountCents { get; init; }
    public Guid? CashBoxId { get; init; }
    public string? Notes { get; init; }
}

public class SkipRecurringExpenseRequest
{
    public required DateOnly PeriodStart { get; init; }
}
