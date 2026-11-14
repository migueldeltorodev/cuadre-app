using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface ICashBoxService
{
    Task<Result<CashBox>> CreateAsync(CashBox cashBox, CancellationToken cancellationToken = default);
    Task<Result<CashBox>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CashBox>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<CashBox>> UpdateAsync(CashBoxChanges changes, CancellationToken cancellationToken = default);
}

public class CashBoxService : ICashBoxService
{
    private readonly ICashBoxRepository _repository;
    private readonly IValidator<CashBox> _validator;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public CashBoxService(ICashBoxRepository repository, IValidator<CashBox> validator, IAuditService audit,
        TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _audit = audit;
        _clock = clock;
    }

    public async Task<Result<CashBox>> CreateAsync(CashBox cashBox, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(cashBox, cancellationToken);

        if (await _repository.GetByNameAsync(cashBox.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya existe una caja llamada '{cashBox.Name}'."));
        }

        var now = _clock.UtcNow();
        cashBox.CreatedAt = now;
        cashBox.UpdatedAt = now;

        await _repository.CreateAsync(cashBox, cancellationToken);
        return Result.Ok(cashBox);
    }

    public async Task<Result<CashBox>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cashBox = await _repository.GetByIdAsync(id, cancellationToken);
        return cashBox is null
            ? Result.Fail(NotFoundError.For<CashBox>(id))
            : Result.Ok(cashBox);
    }

    public Task<IEnumerable<CashBox>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    /// <summary>Only name and active flag change: currency and kind are immutable by design.</summary>
    public async Task<Result<CashBox>> UpdateAsync(CashBoxChanges changes,
        CancellationToken cancellationToken = default)
    {
        var cashBox = await _repository.GetByIdAsync(changes.Id, cancellationToken);
        if (cashBox is null)
        {
            return Result.Fail(NotFoundError.For<CashBox>(changes.Id));
        }

        var isBeingDeactivated = cashBox.IsActive && !changes.IsActive;
        var log = new ChangeList().Text("Nombre", cashBox.Name, changes.Name).Active(cashBox.IsActive, changes.IsActive);
        cashBox.Name = changes.Name;
        cashBox.IsActive = changes.IsActive;

        await _validator.ValidateOrThrowAsync(cashBox, cancellationToken);

        var sameName = await _repository.GetByNameAsync(cashBox.Name, cancellationToken);
        if (sameName is not null && sameName.Id != cashBox.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe una caja llamada '{cashBox.Name}'."));
        }

        // Otherwise money collected with that payment method would land in a box nobody sees.
        if (isBeingDeactivated && await _repository.HasActivePaymentMethodsAsync(cashBox.Id, cancellationToken))
        {
            return Result.Fail(new BusinessRuleError(
                "No se puede desactivar la caja: hay formas de cobro activas que entran a ella. " +
                "Primero desactivalas o pasalas a otra caja."));
        }

        cashBox.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(cashBox, cancellationToken);
        await _audit.RecordAsync(ConfigSubject.CashBox, cashBox.Id, cashBox.Name, log, cancellationToken);
        return Result.Ok(cashBox);
    }
}
