using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface ISupplierService
{
    Task<Result<Supplier>> CreateAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task<Result<Supplier>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<Supplier>> UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
}

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repository;
    private readonly IValidator<Supplier> _validator;
    private readonly TimeProvider _clock;

    public SupplierService(ISupplierRepository repository, IValidator<Supplier> validator, TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<Supplier>> CreateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(supplier, cancellationToken);

        if (await _repository.GetByNameAsync(supplier.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya existe un proveedor llamado '{supplier.Name}'."));
        }

        var now = _clock.UtcNow();
        supplier.CreatedAt = now;
        supplier.UpdatedAt = now;

        await _repository.CreateAsync(supplier, cancellationToken);
        return Result.Ok(supplier);
    }

    public async Task<Result<Supplier>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _repository.GetByIdAsync(id, cancellationToken);
        return supplier is null
            ? Result.Fail(NotFoundError.For<Supplier>(id))
            : Result.Ok(supplier);
    }

    public Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// Deactivating a supplier that is the "usual supplier" of some products is allowed: it only means
    /// "don't offer it for new purchases"; the products keep pointing to it until someone changes them.
    /// </summary>
    public async Task<Result<Supplier>> UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(supplier.Id, cancellationToken);
        if (existing is null)
        {
            return Result.Fail(NotFoundError.For<Supplier>(supplier.Id));
        }

        await _validator.ValidateOrThrowAsync(supplier, cancellationToken);

        var sameName = await _repository.GetByNameAsync(supplier.Name, cancellationToken);
        if (sameName is not null && sameName.Id != supplier.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe un proveedor llamado '{supplier.Name}'."));
        }

        supplier.CreatedAt = existing.CreatedAt;
        supplier.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(supplier, cancellationToken);
        return Result.Ok(supplier);
    }
}
