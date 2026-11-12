using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>Where the goods are (RF-INV-01): create, rename, deactivate. Never deleted.</summary>
public interface ILocationService
{
    Task<Result<Location>> CreateAsync(Location location, CancellationToken cancellationToken = default);
    Task<Result<Location>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<Location>> UpdateAsync(LocationChanges changes, CancellationToken cancellationToken = default);
}

public class LocationService : ILocationService
{
    private readonly ILocationRepository _repository;
    private readonly IValidator<Location> _validator;
    private readonly TimeProvider _clock;

    public LocationService(ILocationRepository repository, IValidator<Location> validator, TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<Result<Location>> CreateAsync(Location location, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(location, cancellationToken);

        if (await _repository.GetByNameAsync(location.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya existe una ubicación llamada '{location.Name}'."));
        }

        var now = _clock.UtcNow();
        location.CreatedAt = now;
        location.UpdatedAt = now;

        await _repository.CreateAsync(location, cancellationToken);
        return Result.Ok(location);
    }

    public async Task<Result<Location>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await _repository.GetByIdAsync(id, cancellationToken);
        return location is null
            ? Result.Fail(NotFoundError.For<Location>(id))
            : Result.Ok(location);
    }

    public Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Result<Location>> UpdateAsync(LocationChanges changes,
        CancellationToken cancellationToken = default)
    {
        var location = await _repository.GetByIdAsync(changes.Id, cancellationToken);
        if (location is null)
        {
            return Result.Fail(NotFoundError.For<Location>(changes.Id));
        }

        var isBeingDeactivated = location.IsActive && !changes.IsActive;
        location.Name = changes.Name;
        location.IsActive = changes.IsActive;

        await _validator.ValidateOrThrowAsync(location, cancellationToken);

        var sameName = await _repository.GetByNameAsync(location.Name, cancellationToken);
        if (sameName is not null && sameName.Id != location.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe una ubicación llamada '{location.Name}'."));
        }

        if (isBeingDeactivated)
        {
            // Goods in a hidden location would vanish from the stock table but not from the totals.
            if (await _repository.HasStockAsync(location.Id, cancellationToken))
            {
                return Result.Fail(new BusinessRuleError(
                    $"No se puede desactivar '{location.Name}': todavía tiene mercancía. Trasladala o dala de baja primero."));
            }

            // Purchases need a Storage and the daily close needs a Counter.
            if (await _repository.CountActiveOfKindAsync(location.Kind, cancellationToken) <= 1)
            {
                return Result.Fail(new BusinessRuleError(
                    $"No se puede desactivar '{location.Name}': es la única ubicación activa de su tipo."));
            }
        }

        location.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(location, cancellationToken);
        return Result.Ok(location);
    }
}
