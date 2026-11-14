using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>D-62: the taxes to suggest setting aside. No delete: they are deactivated.</summary>
public interface ITaxService
{
    Task<Result<Tax>> CreateAsync(Tax tax, CancellationToken cancellationToken = default);
    Task<IEnumerable<Tax>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<Tax>> UpdateAsync(TaxChanges changes, CancellationToken cancellationToken = default);
}

public class TaxService : ITaxService
{
    private readonly ITaxRepository _repository;
    private readonly IValidator<Tax> _validator;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public TaxService(ITaxRepository repository, IValidator<Tax> validator, IAuditService audit, TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _audit = audit;
        _clock = clock;
    }

    public async Task<Result<Tax>> CreateAsync(Tax tax, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(tax, cancellationToken);

        if (await _repository.GetByNameAsync(tax.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya existe el impuesto '{tax.Name}'."));
        }

        var now = _clock.UtcNow();
        tax.CreatedAt = now;
        tax.UpdatedAt = now;
        await _repository.CreateAsync(tax, cancellationToken);
        return Result.Ok(tax);
    }

    public Task<IEnumerable<Tax>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Result<Tax>> UpdateAsync(TaxChanges changes, CancellationToken cancellationToken = default)
    {
        var tax = await _repository.GetByIdAsync(changes.Id, cancellationToken);
        if (tax is null)
        {
            return Result.Fail(NotFoundError.For<Tax>(changes.Id));
        }

        var log = new ChangeList()
            .Text("Nombre", tax.Name, changes.Name)
            .Percent("Tasa", tax.RateBps, changes.RateBps)
            .Text("Se calcula sobre", BaseText(tax.Base), BaseText(changes.Base))
            .Active(tax.IsActive, changes.IsActive);
        tax.Name = changes.Name;
        tax.RateBps = changes.RateBps;
        tax.Base = changes.Base;
        tax.IsActive = changes.IsActive;

        await _validator.ValidateOrThrowAsync(tax, cancellationToken);

        var sameName = await _repository.GetByNameAsync(tax.Name, cancellationToken);
        if (sameName is not null && sameName.Id != tax.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe el impuesto '{tax.Name}'."));
        }

        tax.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(tax, cancellationToken);
        await _audit.RecordAsync(ConfigSubject.Tax, tax.Id, tax.Name, log, cancellationToken);
        return Result.Ok(tax);
    }

    private static string BaseText(TaxBase taxBase) => taxBase == TaxBase.Sales ? "Ventas" : "Ganancia";
}
