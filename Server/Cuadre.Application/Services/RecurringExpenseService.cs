using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>RF-GAS-01: the templates, the occurrences already due, and confirming or skipping each one.</summary>
public interface IRecurringExpenseService
{
    Task<Result<RecurringExpense>> CreateAsync(RecurringExpense recurring, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecurringExpense>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Everything editable. The expenses already confirmed keep their own data.</summary>
    Task<Result<RecurringExpense>> UpdateAsync(RecurringExpense recurring,
        CancellationToken cancellationToken = default);

    /// <summary>The occurrences of the active templates due up to today, not confirmed nor skipped. Oldest first.</summary>
    Task<IReadOnlyList<DueExpense>> GetDueAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the expense of that occurrence (same rules as any expense).</summary>
    Task<Result<ExpenseResult>> ConfirmAsync(ConfirmRecurringExpense request,
        CancellationToken cancellationToken = default);

    /// <summary>"This time it was not paid": that occurrence is not proposed any more. No money moves.</summary>
    Task<Result> SkipAsync(SkipRecurringExpense request, CancellationToken cancellationToken = default);
}

public class RecurringExpenseService : IRecurringExpenseService
{
    private readonly IRecurringExpenseRepository _repository;
    private readonly IExpenseCategoryRepository _categoryRepository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IExpenseService _expenseService;
    private readonly IValidator<RecurringExpense> _validator;
    private readonly IValidator<ConfirmRecurringExpense> _confirmValidator;
    private readonly IValidator<SkipRecurringExpense> _skipValidator;
    private readonly TimeProvider _clock;

    public RecurringExpenseService(IRecurringExpenseRepository repository,
        IExpenseCategoryRepository categoryRepository, IEnvelopeRepository envelopeRepository,
        ICashBoxRepository cashBoxRepository, IExpenseService expenseService, IValidator<RecurringExpense> validator,
        IValidator<ConfirmRecurringExpense> confirmValidator, IValidator<SkipRecurringExpense> skipValidator,
        TimeProvider clock)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _envelopeRepository = envelopeRepository;
        _cashBoxRepository = cashBoxRepository;
        _expenseService = expenseService;
        _validator = validator;
        _confirmValidator = confirmValidator;
        _skipValidator = skipValidator;
        _clock = clock;
    }

    public async Task<Result<RecurringExpense>> CreateAsync(RecurringExpense recurring,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(recurring, cancellationToken);

        var check = await CheckReferencesAsync(recurring, cancellationToken);
        if (check.IsFailed)
        {
            return check;
        }

        var now = _clock.UtcNow();
        recurring.IsActive = true;
        recurring.CreatedAt = now;
        recurring.UpdatedAt = now;
        await _repository.CreateAsync(recurring, cancellationToken);

        return Result.Ok(await _repository.GetByIdAsync(recurring.Id, cancellationToken) ?? recurring);
    }

    public Task<IEnumerable<RecurringExpense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Result<RecurringExpense>> UpdateAsync(RecurringExpense recurring,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(recurring.Id, cancellationToken);
        if (existing is null)
        {
            return Result.Fail(NotFoundError.For<RecurringExpense>(recurring.Id));
        }

        await _validator.ValidateOrThrowAsync(recurring, cancellationToken);

        // A deactivated template is not proposed: its references may be inactive by now, so only check them
        // when it stays (or becomes) active.
        if (recurring.IsActive)
        {
            var check = await CheckReferencesAsync(recurring, cancellationToken);
            if (check.IsFailed)
            {
                return check;
            }
        }
        else
        {
            var sameName = await _repository.GetByNameAsync(recurring.Name, cancellationToken);
            if (sameName is not null && sameName.Id != recurring.Id)
            {
                return Result.Fail(new ConflictError($"Ya hay un gasto fijo llamado '{recurring.Name}'."));
            }

            recurring.EnvelopeId ??= existing.EnvelopeId;
        }

        recurring.CreatedAt = existing.CreatedAt;
        recurring.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(recurring, cancellationToken);

        return Result.Ok(await _repository.GetByIdAsync(recurring.Id, cancellationToken) ?? recurring);
    }

    public async Task<IReadOnlyList<DueExpense>> GetDueAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today();
        var taken = (await _repository.GetTakenPeriodsAsync(cancellationToken: cancellationToken))
            .ToLookup(period => period.RecurringExpenseId, period => period.PeriodStart);

        return (await _repository.GetAllAsync(cancellationToken))
            .Where(recurring => recurring.IsActive)
            .SelectMany(recurring => DueOf(recurring, today, taken[recurring.Id].ToHashSet())
                .Select(due => new DueExpense(recurring, due.PeriodStart, due.DueDate)))
            .OrderBy(due => due.DueDate)
            .ThenBy(due => due.Recurring.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Result<ExpenseResult>> ConfirmAsync(ConfirmRecurringExpense request,
        CancellationToken cancellationToken = default)
    {
        var recurring = await _repository.GetByIdAsync(request.RecurringExpenseId, cancellationToken);
        if (recurring is null)
        {
            return Result.Fail(NotFoundError.For<RecurringExpense>(request.RecurringExpenseId));
        }

        await _confirmValidator.ValidateOrThrowAsync(request, cancellationToken);

        var due = await EnsureDueAsync(recurring, request.PeriodStart, cancellationToken);
        if (due.IsFailed)
        {
            return due;
        }

        return await _expenseService.CreateAsync(new Expense
        {
            Id = Guid.NewGuid(),
            Date = request.Date ?? _clock.Today(),
            CategoryId = recurring.CategoryId,
            Description = recurring.Name,
            AmountCents = request.AmountCents ?? recurring.AmountCents,
            CashBoxId = request.CashBoxId ?? recurring.CashBoxId,
            EnvelopeId = recurring.EnvelopeId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecurringExpenseId = recurring.Id,
            PeriodStart = request.PeriodStart
        }, cancellationToken);
    }

    public async Task<Result> SkipAsync(SkipRecurringExpense request, CancellationToken cancellationToken = default)
    {
        var recurring = await _repository.GetByIdAsync(request.RecurringExpenseId, cancellationToken);
        if (recurring is null)
        {
            return Result.Fail(NotFoundError.For<RecurringExpense>(request.RecurringExpenseId));
        }

        await _skipValidator.ValidateOrThrowAsync(request, cancellationToken);

        var due = await EnsureDueAsync(recurring, request.PeriodStart, cancellationToken);
        if (due.IsFailed)
        {
            return due;
        }

        await _repository.SkipAsync(request, _clock.UtcNow(), cancellationToken);
        return Result.Ok();
    }

    /// <summary>Only an occurrence the app is proposing right now can be confirmed or skipped.</summary>
    private async Task<Result> EnsureDueAsync(RecurringExpense recurring, DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        if (!recurring.IsActive)
        {
            return Result.Fail(new BusinessRuleError($"El gasto fijo '{recurring.Name}' está desactivado."));
        }

        var taken = (await _repository.GetTakenPeriodsAsync(recurring.Id, cancellationToken))
            .Select(period => period.PeriodStart)
            .ToHashSet();

        return DueOf(recurring, _clock.Today(), taken).Any(due => due.PeriodStart == periodStart)
            ? Result.Ok()
            : Result.Fail(new BusinessRuleError(
                $"'{recurring.Name}' no tiene pendiente el período del {ClosedDayGuard.Format(periodStart)}: " +
                "ya se pagó, se saltó o todavía no venció."));
    }

    private static IReadOnlyList<(DateOnly PeriodStart, DateOnly DueDate)> DueOf(RecurringExpense recurring,
        DateOnly today, IReadOnlySet<DateOnly> taken) =>
        RecurringSchedule.Due(recurring.Frequency, recurring.DueDay, recurring.StartsOn, today, taken);

    /// <summary>Name unique (409); category, box and envelope usable (422). Resolves a null envelope to Operación.</summary>
    private async Task<Result> CheckReferencesAsync(RecurringExpense recurring, CancellationToken cancellationToken)
    {
        var sameName = await _repository.GetByNameAsync(recurring.Name, cancellationToken);
        if (sameName is not null && sameName.Id != recurring.Id)
        {
            return Result.Fail(new ConflictError($"Ya hay un gasto fijo llamado '{recurring.Name}'."));
        }

        var category = await _categoryRepository.GetByIdAsync(recurring.CategoryId, cancellationToken);
        if (category is null || !category.IsActive)
        {
            return Result.Fail(new BusinessRuleError("La categoría elegida no existe o está desactivada."));
        }

        var box = await _cashBoxRepository.GetByIdAsync(recurring.CashBoxId, cancellationToken);
        if (box is null || !box.IsActive)
        {
            return Result.Fail(new BusinessRuleError("La caja elegida no existe o está desactivada."));
        }

        var envelope = await ExpenseService.ResolveEnvelopeAsync(_envelopeRepository, recurring.EnvelopeId,
            cancellationToken);
        if (envelope.IsFailed)
        {
            return Result.Fail(envelope.Errors);
        }

        recurring.EnvelopeId = envelope.Value.Id;
        return Result.Ok();
    }
}
