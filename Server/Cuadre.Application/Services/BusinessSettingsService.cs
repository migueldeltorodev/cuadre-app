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
    private readonly TimeProvider _clock;

    public BusinessSettingsService(IBusinessSettingsRepository repository, IValidator<BusinessSettings> validator,
        TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
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
        settings.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(settings, cancellationToken);

        return await GetAsync(cancellationToken);
    }
}
