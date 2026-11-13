using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 7: expense categories, expenses and recurring expenses.</summary>
public static partial class ContractMapping
{
    // ---------- Expense categories ----------

    public static ExpenseCategory MapToExpenseCategory(this CreateExpenseCategoryRequest request)
    {
        return new ExpenseCategory
        {
            Id = Guid.NewGuid(),
            Name = request.Name?.Trim() ?? ""
        };
    }

    public static ExpenseCategoryChanges MapToExpenseCategoryChanges(this UpdateExpenseCategoryRequest request,
        Guid id)
    {
        return new ExpenseCategoryChanges(id, request.Name?.Trim() ?? "", request.IsActive);
    }

    public static ExpenseCategoryResponse MapToResponse(this ExpenseCategory category)
    {
        return new ExpenseCategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            IsActive = category.IsActive,
            ExpenseCount = category.ExpenseCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    public static ExpenseCategoriesResponse MapToResponse(this IEnumerable<ExpenseCategory> categories)
    {
        return new ExpenseCategoriesResponse { Items = categories.Select(category => category.MapToResponse()) };
    }

    // ---------- Expenses ----------

    public static Expense MapToExpense(this CreateExpenseRequest request)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            CategoryId = request.CategoryId,
            Description = request.Description?.Trim() ?? "",
            AmountCents = request.AmountCents,
            CashBoxId = request.CashBoxId,
            EnvelopeId = request.EnvelopeId,
            Notes = BlankToNull(request.Notes)
        };
    }

    public static VoidExpense MapToVoidExpense(this VoidExpenseRequest request, Guid id)
    {
        return new VoidExpense(id, request.Reason ?? "");
    }

    public static ExpenseResponse MapToResponse(this ExpenseResult result)
    {
        return result.Expense.MapToResponse(result.Warning);
    }

    public static ExpenseResponse MapToResponse(this Expense expense, NegativeEnvelopeWarning? warning = null)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            Number = expense.Number,
            Date = expense.Date,
            CategoryId = expense.CategoryId,
            CategoryName = expense.CategoryName,
            Description = expense.Description,
            AmountCents = expense.AmountCents,
            CashBoxId = expense.CashBoxId,
            CashBoxName = expense.CashBoxName,
            Currency = expense.Currency.ToContract(),
            EnvelopeId = expense.EnvelopeId.GetValueOrDefault(),
            EnvelopeName = expense.EnvelopeName,
            Notes = expense.Notes,
            RecurringExpenseId = expense.RecurringExpenseId,
            RecurringExpenseName = expense.RecurringExpenseName,
            PeriodStart = expense.PeriodStart,
            Status = expense.Status.ToContract(),
            VoidReason = expense.VoidReason,
            VoidedAt = expense.VoidedAt,
            Warning = warning is null
                ? null
                : new NegativeEnvelopeWarningResponse
                {
                    EnvelopeId = warning.EnvelopeId,
                    EnvelopeName = warning.EnvelopeName,
                    CashBoxId = warning.CashBoxId,
                    CashBoxName = warning.CashBoxName,
                    Currency = warning.Currency.ToContract(),
                    BalanceCents = warning.BalanceCents
                },
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
    }

    private static ExpenseTotalResponse MapToResponse(this ExpenseTotal total) => new()
    {
        Currency = total.Currency.ToContract(),
        AmountCents = total.AmountCents
    };

    public static ExpensesResponse MapToResponse(this IEnumerable<Expense> expenses, IEnumerable<ExpenseTotal> totals,
        int page, int pageSize, int total)
    {
        return new ExpensesResponse
        {
            Items = expenses.Select(expense => expense.MapToResponse()),
            Totals = totals.Select(total => total.MapToResponse()).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static GetAllExpensesOptions MapToOptions(this GetAllExpensesRequest request)
    {
        return new GetAllExpensesOptions
        {
            From = request.From,
            To = request.To,
            CategoryId = request.CategoryId,
            CashBoxId = request.CashBoxId,
            EnvelopeId = request.EnvelopeId,
            RecurringExpenseId = request.RecurringExpenseId,
            IncludeVoided = request.IncludeVoided.GetValueOrDefault(),
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    // ---------- Recurring expenses ----------

    public static RecurringExpense MapToRecurringExpense(this CreateRecurringExpenseRequest request)
    {
        return new RecurringExpense
        {
            Id = Guid.NewGuid(),
            Name = request.Name?.Trim() ?? "",
            CategoryId = request.CategoryId,
            AmountCents = request.AmountCents,
            CashBoxId = request.CashBoxId,
            EnvelopeId = request.EnvelopeId,
            Frequency = request.Frequency.ToModel(),
            DueDay = request.DueDay,
            StartsOn = request.StartsOn,
            Notes = BlankToNull(request.Notes)
        };
    }

    public static RecurringExpense MapToRecurringExpense(this UpdateRecurringExpenseRequest request, Guid id)
    {
        return new RecurringExpense
        {
            Id = id,
            Name = request.Name?.Trim() ?? "",
            CategoryId = request.CategoryId,
            AmountCents = request.AmountCents,
            CashBoxId = request.CashBoxId,
            EnvelopeId = request.EnvelopeId,
            Frequency = request.Frequency.ToModel(),
            DueDay = request.DueDay,
            StartsOn = request.StartsOn,
            Notes = BlankToNull(request.Notes),
            IsActive = request.IsActive
        };
    }

    public static RecurringExpenseResponse MapToResponse(this RecurringExpense recurring)
    {
        return new RecurringExpenseResponse
        {
            Id = recurring.Id,
            Name = recurring.Name,
            CategoryId = recurring.CategoryId,
            CategoryName = recurring.CategoryName,
            AmountCents = recurring.AmountCents,
            CashBoxId = recurring.CashBoxId,
            CashBoxName = recurring.CashBoxName,
            Currency = recurring.Currency.ToContract(),
            EnvelopeId = recurring.EnvelopeId.GetValueOrDefault(),
            EnvelopeName = recurring.EnvelopeName,
            Frequency = recurring.Frequency.ToContract(),
            DueDay = recurring.DueDay,
            StartsOn = recurring.StartsOn,
            Notes = recurring.Notes,
            IsActive = recurring.IsActive,
            CreatedAt = recurring.CreatedAt,
            UpdatedAt = recurring.UpdatedAt
        };
    }

    public static RecurringExpensesResponse MapToResponse(this IEnumerable<RecurringExpense> recurring)
    {
        return new RecurringExpensesResponse { Items = recurring.Select(item => item.MapToResponse()) };
    }

    public static DueExpensesResponse MapToResponse(this IEnumerable<DueExpense> due)
    {
        return new DueExpensesResponse
        {
            Items = due.Select(item => new DueExpenseResponse
            {
                Recurring = item.Recurring.MapToResponse(),
                PeriodStart = item.PeriodStart,
                DueDate = item.DueDate
            })
        };
    }

    public static ConfirmRecurringExpense MapToConfirmRecurringExpense(this ConfirmRecurringExpenseRequest request,
        Guid id)
    {
        return new ConfirmRecurringExpense(id, request.PeriodStart, request.Date, request.AmountCents,
            request.CashBoxId, request.Notes);
    }

    public static SkipRecurringExpense MapToSkipRecurringExpense(this SkipRecurringExpenseRequest request, Guid id)
    {
        return new SkipRecurringExpense(id, request.PeriodStart);
    }

    // ---------- Enums ----------

    private static ExpenseState ToContract(this ExpenseStatus status) => status switch
    {
        ExpenseStatus.Confirmed => ExpenseState.Confirmed,
        ExpenseStatus.Voided => ExpenseState.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static RecurringFrequency ToModel(this ExpenseFrequency frequency) => frequency switch
    {
        ExpenseFrequency.Monthly => RecurringFrequency.Monthly,
        ExpenseFrequency.Weekly => RecurringFrequency.Weekly,
        _ => (RecurringFrequency)(int)frequency
    };

    private static ExpenseFrequency ToContract(this RecurringFrequency frequency) => frequency switch
    {
        RecurringFrequency.Monthly => ExpenseFrequency.Monthly,
        RecurringFrequency.Weekly => ExpenseFrequency.Weekly,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
    };
}
