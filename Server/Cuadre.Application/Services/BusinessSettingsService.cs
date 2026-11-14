using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IBusinessSettingsService
{
    Task<BusinessSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<BusinessSettings> UpdateAsync(BusinessSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// The settings row always exists (DatabaseSeeder creates it at startup), so there is no NotFound path:
/// a missing row is a broken installation and surfaces as a 500, not as a business outcome.
/// </summary>
public class BusinessSettingsService : IBusinessSettingsService
{
    private readonly IBusinessSettingsRepository _repository;
    private readonly IValidator<BusinessSettings> _validator;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public BusinessSettingsService(IBusinessSettingsRepository repository, IValidator<BusinessSettings> validator,
        IAuditService audit, TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _audit = audit;
        _clock = clock;
    }

    public async Task<BusinessSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAsync(cancellationToken)
               ?? throw new InvalidOperationException("business_settings has no row: the database was not seeded");
    }

    public async Task<BusinessSettings> UpdateAsync(BusinessSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(settings, cancellationToken);

        settings.ClosedDays = settings.ClosedDays.Distinct().Order().ToArray();
        var before = await GetAsync(cancellationToken);
        settings.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(settings, cancellationToken);

        // RF-DAT-08.
        await _audit.RecordAsync(ConfigSubject.Settings, null, settings.Name, new ChangeList()
            .Text("Nombre del negocio", before.Name, settings.Name)
            .Text("Dirección", before.Address, settings.Address)
            .Text("Días sin cierre", AuditFormat.Days(before.ClosedDays), AuditFormat.Days(settings.ClosedDays))
            .Money("Diferencia de caja que pide nota", before.CashDifferenceThresholdCents,
                settings.CashDifferenceThresholdCents)
            .Money("Colchón sobre la tasa", before.ExchangeBufferCents, settings.ExchangeBufferCents)
            .Percent("Recargo por defecto", before.DefaultMarkupBps, settings.DefaultMarkupBps)
            .Money("Redondear precios a", before.PriceRoundingStepCents, settings.PriceRoundingStepCents),
            cancellationToken);

        return await GetAsync(cancellationToken);
    }
}
