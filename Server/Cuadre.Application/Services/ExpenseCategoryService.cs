using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>RF-GAS-03. No delete: expenses point to the categories, so they are deactivated.</summary>
public interface IExpenseCategoryService
{
    Task<Result<ExpenseCategory>> CreateAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<ExpenseCategory>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Rename or (de)activate.</summary>
    Task<Result<ExpenseCategory>> UpdateAsync(ExpenseCategoryChanges changes,
        CancellationToken cancellationToken = default);
}

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IExpenseCategoryRepository _repository;
    private readonly IRecurringExpenseRepository _recurringRepository;
    private readonly IValidator<ExpenseCategory> _validator;
    private readonly TimeProvider _clock;

    public ExpenseCategoryService(IExpenseCategoryRepository repository,
        IRecurringExpenseRepository recurringRepository, IValidator<ExpenseCategory> validator, TimeProvider clock)
    {
        _repository = repository;
        _recurringRepository = recurringRepository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<ExpenseCategory>> CreateAsync(ExpenseCategory category,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(category, cancellationToken);

        if (await _repository.GetByNameAsync(category.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya existe la categoría '{category.Name}'."));
        }

        var now = _clock.UtcNow();
        category.IsActive = true;
        category.CreatedAt = now;
        category.UpdatedAt = now;
        await _repository.CreateAsync(category, cancellationToken);
        return Result.Ok(category);
    }

    public Task<IEnumerable<ExpenseCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Result<ExpenseCategory>> UpdateAsync(ExpenseCategoryChanges changes,
        CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(changes.Id, cancellationToken);
        if (category is null)
        {
            return Result.Fail(NotFoundError.For<ExpenseCategory>(changes.Id));
        }

        var deactivating = category.IsActive && !changes.IsActive;
        category.Name = changes.Name;
        category.IsActive = changes.IsActive;

        await _validator.ValidateOrThrowAsync(category, cancellationToken);

        var sameName = await _repository.GetByNameAsync(category.Name, cancellationToken);
        if (sameName is not null && sameName.Id != category.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe la categoría '{category.Name}'."));
        }

        // An active recurring expense would be proposed with a category nobody can use any more.
        if (deactivating)
        {
            var user = (await _recurringRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(recurring => recurring.IsActive && recurring.CategoryId == category.Id);
            if (user is not null)
            {
                return Result.Fail(new BusinessRuleError(
                    $"No se puede desactivar: la usa el gasto fijo '{user.Name}'. Cambiale la categoría o desactivalo."));
            }
        }

        category.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(category, cancellationToken);
        return Result.Ok(category);
    }
}
