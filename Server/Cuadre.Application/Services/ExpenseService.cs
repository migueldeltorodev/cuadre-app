using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IExpenseService
{
    /// <summary>RF-GAS-02: validates, confirms (money out of the box and the envelope), warns if it goes negative.</summary>
    Task<Result<ExpenseResult>> CreateAsync(Expense expense, CancellationToken cancellationToken = default);

    Task<Result<Expense>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<Expense>> GetAllAsync(GetAllExpensesOptions options, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllExpensesOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseTotal>> GetTotalsAsync(GetAllExpensesOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>RF-GAS-04: the money comes back to its envelope with a reversal dated today.</summary>
    Task<Result<Expense>> VoidAsync(VoidExpense request, CancellationToken cancellationToken = default);
}

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _repository;
    private readonly IExpenseCategoryRepository _categoryRepository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IValidator<Expense> _validator;
    private readonly IValidator<VoidExpense> _voidValidator;
    private readonly IValidator<GetAllExpensesOptions> _optionsValidator;
    private readonly IClosedDayGuard _closedDayGuard;
    private readonly TimeProvider _clock;

    public ExpenseService(IExpenseRepository repository, IExpenseCategoryRepository categoryRepository,
        IEnvelopeRepository envelopeRepository, ICashBoxRepository cashBoxRepository, IValidator<Expense> validator,
        IValidator<VoidExpense> voidValidator, IValidator<GetAllExpensesOptions> optionsValidator,
        IClosedDayGuard closedDayGuard, TimeProvider clock)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _envelopeRepository = envelopeRepository;
        _cashBoxRepository = cashBoxRepository;
        _validator = validator;
        _voidValidator = voidValidator;
        _optionsValidator = optionsValidator;
        _closedDayGuard = closedDayGuard;
        _clock = clock;
    }

    public async Task<Result<ExpenseResult>> CreateAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(expense, cancellationToken);

        if (expense.Date > _clock.Today())
        {
            return Result.Fail(new BusinessRuleError("La fecha no puede ser futura."));
        }

        var open = await _closedDayGuard.EnsureOpenAsync(expense.Date, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var category = await _categoryRepository.GetByIdAsync(expense.CategoryId, cancellationToken);
        if (category is null || !category.IsActive)
        {
            return Result.Fail(new BusinessRuleError("La categoría elegida no existe o está desactivada."));
        }

        var box = await _cashBoxRepository.GetByIdAsync(expense.CashBoxId, cancellationToken);
        if (box is null || !box.IsActive)
        {
            return Result.Fail(new BusinessRuleError("La caja elegida no existe o está desactivada."));
        }

        var envelope = await ResolveEnvelopeAsync(_envelopeRepository, expense.EnvelopeId, cancellationToken);
        if (envelope.IsFailed)
        {
            return Result.Fail(envelope.Errors);
        }

        expense.EnvelopeId = envelope.Value.Id;

        // Before saving: what the envelope holds in that box now, to warn if the expense leaves it negative.
        var before = (await _envelopeRepository.GetBalancesAsync(envelopeId: envelope.Value.Id,
                cancellationToken: cancellationToken))
            .FirstOrDefault(balance => balance.CashBoxId == box.Id)?.AmountCents ?? 0;

        var now = _clock.UtcNow();
        expense.Status = ExpenseStatus.Confirmed;
        expense.CreatedAt = now;
        expense.UpdatedAt = now;
        expense.Number = await _repository.GetNextNumberAsync(cancellationToken);

        var movement = NewMovement(expense, -expense.AmountCents, expense.Date, CashMovementKind.Expense,
            $"Gasto #{expense.Number}: {expense.Description.Trim()}", now);

        await _repository.CreateAsync(expense, [movement], cancellationToken);

        var saved = await _repository.GetByIdAsync(expense.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The expense was not found right after saving it.");

        var after = before - saved.AmountCents;
        var warning = after < 0
            ? new NegativeEnvelopeWarning(saved.EnvelopeId!.Value, saved.EnvelopeName, saved.CashBoxId,
                saved.CashBoxName, saved.Currency, after)
            : null;

        return Result.Ok(new ExpenseResult(saved, warning));
    }

    public async Task<Result<Expense>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(id, cancellationToken);
        return expense is null
            ? Result.Fail(NotFoundError.For<Expense>(id))
            : Result.Ok(expense);
    }

    public async Task<IEnumerable<Expense>> GetAllAsync(GetAllExpensesOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllExpensesOptions options, CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }

    public Task<IReadOnlyList<ExpenseTotal>> GetTotalsAsync(GetAllExpensesOptions options,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetTotalsAsync(options, cancellationToken);
    }

    public async Task<Result<Expense>> VoidAsync(VoidExpense request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(request.ExpenseId, cancellationToken);
        if (expense is null)
        {
            return Result.Fail(NotFoundError.For<Expense>(request.ExpenseId));
        }

        await _voidValidator.ValidateOrThrowAsync(request, cancellationToken);

        if (expense.Status == ExpenseStatus.Voided)
        {
            return Result.Fail(new BusinessRuleError($"El gasto #{expense.Number} ya está anulado."));
        }

        // The reversal is dated today: today must still be open.
        var today = _clock.Today();
        var open = await _closedDayGuard.EnsureOpenAsync(today, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var now = _clock.UtcNow();
        expense.Status = ExpenseStatus.Voided;
        expense.VoidReason = request.Reason.Trim();
        expense.VoidedAt = now;
        expense.UpdatedAt = now;

        var reversal = NewMovement(expense, expense.AmountCents, today, CashMovementKind.ExpenseVoid,
            $"Anulación del gasto #{expense.Number}", now);

        await _repository.VoidAsync(expense, [reversal], cancellationToken);
        return Result.Ok(await _repository.GetByIdAsync(expense.Id, cancellationToken) ?? expense);
    }

    /// <summary>
    /// Null = Operación (D-40). Any other active envelope may pay an expense, except Reposición: it only pays
    /// merchandise, so A2 stays the cost of what was sold (D-01).
    /// </summary>
    internal static async Task<Result<Envelope>> ResolveEnvelopeAsync(IEnvelopeRepository envelopeRepository,
        Guid? envelopeId, CancellationToken cancellationToken)
    {
        var envelope = envelopeId is { } id
            ? await envelopeRepository.GetByIdAsync(id, cancellationToken)
            : await envelopeRepository.GetByRoleAsync(EnvelopeRole.Operations, cancellationToken);

        if (envelope is null || !envelope.IsActive)
        {
            return Result.Fail(new BusinessRuleError("El sobre elegido no existe o está desactivado."));
        }

        return envelope.Role == EnvelopeRole.Replenishment
            ? Result.Fail(new BusinessRuleError(
                $"Los gastos no salen de '{envelope.Name}': ese sobre solo paga mercadería. Usá Gastos de operación."))
            : Result.Ok(envelope);
    }

    private static CashMovement NewMovement(Expense expense, long amountCents, DateOnly date, CashMovementKind kind,
        string description, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CashBoxId = expense.CashBoxId,
        EnvelopeId = expense.EnvelopeId!.Value,
        Date = date,
        Kind = kind,
        AmountCents = amountCents,
        Description = description,
        ExpenseId = expense.Id,
        CreatedAt = now
    };
}
