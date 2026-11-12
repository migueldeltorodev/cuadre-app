using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface ICategoryService
{
    Task<Result<Category>> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<Result<Category>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<Category>> UpdateAsync(CategoryChanges changes, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IValidator<Category> _validator;
    private readonly TimeProvider _clock;

    public CategoryService(ICategoryRepository repository, IValidator<Category> validator, TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<Category>> CreateAsync(Category category, CancellationToken cancellationToken = default)
    {
        // Subcategories never carry a prefix; categories get one derived from the name if none was typed.
        category.CodePrefix = category.IsTopLevel
            ? NormalizePrefix(category.CodePrefix) ?? Category.DerivePrefix(category.Name)
            : category.CodePrefix;

        await _validator.ValidateOrThrowAsync(category, cancellationToken);

        if (!category.IsTopLevel)
        {
            var parent = await _repository.GetByIdAsync(category.ParentId!.Value, cancellationToken);
            if (parent is null)
            {
                return Result.Fail(new BusinessRuleError("La categoría elegida no existe."));
            }

            // Two levels only (RF-PRD-04): a subcategory cannot hang from another subcategory.
            if (!parent.IsTopLevel)
            {
                return Result.Fail(new BusinessRuleError(
                    "Solo hay dos niveles: una subcategoría no puede tener subcategorías."));
            }
        }

        var ruleCheck = await CheckUniquenessAsync(category, cancellationToken);
        if (ruleCheck.IsFailed)
        {
            return ruleCheck;
        }

        var now = _clock.UtcNow();
        category.CreatedAt = now;
        category.UpdatedAt = now;

        await _repository.CreateAsync(category, cancellationToken);
        return Result.Ok(category);
    }

    public async Task<Result<Category>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken);
        return category is null
            ? Result.Fail(NotFoundError.For<Category>(id))
            : Result.Ok(category);
    }

    public Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// Name and prefix only; the level never changes. Changing the prefix does NOT rename existing
    /// product codes (they may already be on shelf labels): only new products use the new prefix.
    /// </summary>
    public async Task<Result<Category>> UpdateAsync(CategoryChanges changes,
        CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(changes.Id, cancellationToken);
        if (category is null)
        {
            return Result.Fail(NotFoundError.For<Category>(changes.Id));
        }

        category.Name = changes.Name;
        category.CodePrefix = category.IsTopLevel
            ? NormalizePrefix(changes.CodePrefix) ?? category.CodePrefix
            : changes.CodePrefix;

        await _validator.ValidateOrThrowAsync(category, cancellationToken);

        var ruleCheck = await CheckUniquenessAsync(category, cancellationToken);
        if (ruleCheck.IsFailed)
        {
            return ruleCheck;
        }

        category.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(category, cancellationToken);
        return Result.Ok(category);
    }

    /// <summary>Only while unused: a category with subcategories or products is kept (422).</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Fail(NotFoundError.For<Category>(id));
        }

        if (await _repository.IsInUseAsync(id, cancellationToken))
        {
            return Result.Fail(new BusinessRuleError(
                $"No se puede borrar '{category.Name}': tiene subcategorías o productos. Primero movelos."));
        }

        await _repository.DeleteAsync(id, cancellationToken);
        return Result.Ok();
    }

    /// <summary>Name unique among siblings, prefix unique overall (409).</summary>
    private async Task<Result<Category>> CheckUniquenessAsync(Category category, CancellationToken cancellationToken)
    {
        var sameName = await _repository.GetByNameAsync(category.ParentId, category.Name, cancellationToken);
        if (sameName is not null && sameName.Id != category.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe '{category.Name}' en ese nivel."));
        }

        if (category.CodePrefix is not null)
        {
            var samePrefix = await _repository.GetByPrefixAsync(category.CodePrefix, cancellationToken);
            if (samePrefix is not null && samePrefix.Id != category.Id)
            {
                return Result.Fail(new ConflictError(
                    $"El prefijo '{category.CodePrefix}' ya lo usa '{samePrefix.Name}'. Elegí otro."));
            }
        }

        return Result.Ok(category);
    }

    private static string? NormalizePrefix(string? prefix) =>
        string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim().ToUpperInvariant();
}
