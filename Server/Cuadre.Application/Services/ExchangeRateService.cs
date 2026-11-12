using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IExchangeRateService
{
    /// <summary>Sets the rate of a date, creating or correcting it (one rate per day).</summary>
    Task<Result<ExchangeRate>> UpsertAsync(DateOnly date, long cupPerUsdCents,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRate>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<CurrentExchangeRate> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<ExchangeRate>> GetAllAsync(GetAllExchangeRatesOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllExchangeRatesOptions options, CancellationToken cancellationToken = default);
}

public class ExchangeRateService : IExchangeRateService
{
    private readonly IExchangeRateRepository _repository;
    private readonly IValidator<ExchangeRate> _validator;
    private readonly IValidator<GetAllExchangeRatesOptions> _optionsValidator;
    private readonly TimeProvider _clock;

    public ExchangeRateService(IExchangeRateRepository repository, IValidator<ExchangeRate> validator,
        IValidator<GetAllExchangeRatesOptions> optionsValidator, TimeProvider clock)
    {
        _repository = repository;
        _validator = validator;
        _optionsValidator = optionsValidator;
        _clock = clock;
    }

    public async Task<Result<ExchangeRate>> UpsertAsync(DateOnly date, long cupPerUsdCents,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow();
        var rate = new ExchangeRate { Date = date, CupPerUsdCents = cupPerUsdCents, CreatedAt = now, UpdatedAt = now };

        await _validator.ValidateOrThrowAsync(rate, cancellationToken);

        if (date > _clock.Today())
        {
            return Result.Fail(new BusinessRuleError("No se puede cargar la tasa de un día que todavía no llegó."));
        }

        await _repository.UpsertAsync(rate, cancellationToken);

        // Re-read: on a correction the stored created_at is the original one.
        var stored = await _repository.GetByDateAsync(date, cancellationToken);
        return Result.Ok(stored!);
    }

    public async Task<Result<ExchangeRate>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var rate = await _repository.GetByDateAsync(date, cancellationToken);
        return rate is null
            ? Result.Fail(new NotFoundError($"No hay tasa cargada para el {date:yyyy-MM-dd}."))
            : Result.Ok(rate);
    }

    public async Task<CurrentExchangeRate> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today();
        var latest = await _repository.GetLatestOnOrBeforeAsync(today, cancellationToken);
        return new CurrentExchangeRate(today, latest);
    }

    public async Task<IEnumerable<ExchangeRate>> GetAllAsync(GetAllExchangeRatesOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllExchangeRatesOptions options, CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }
}
