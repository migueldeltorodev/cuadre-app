using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>RF-PF-10: A1, A2, A3 of a period and the real % of each envelope against the plan.</summary>
public interface IEvaluationService
{
    /// <summary>Without dates: from the first day of this month to today.</summary>
    Task<ProfitFirstEvaluation> GetAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}

public class EvaluationService : IEvaluationService
{
    private readonly IEvaluationRepository _repository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly IDailyClosingRepository _closingRepository;
    private readonly IValidator<GetEvaluationOptions> _validator;
    private readonly TimeProvider _clock;

    public EvaluationService(IEvaluationRepository repository, IEnvelopeRepository envelopeRepository,
        IDailyClosingRepository closingRepository, IValidator<GetEvaluationOptions> validator, TimeProvider clock)
    {
        _repository = repository;
        _envelopeRepository = envelopeRepository;
        _closingRepository = closingRepository;
        _validator = validator;
        _clock = clock;
    }

    public async Task<ProfitFirstEvaluation> GetAsync(DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var today = _clock.Today();
        var options = new GetEvaluationOptions
        {
            From = from ?? new DateOnly(today.Year, today.Month, 1),
            To = to ?? today
        };
        await _validator.ValidateOrThrowAsync(options, cancellationToken);

        var totals = await _repository.GetTotalsAsync(options.From, options.To, cancellationToken);
        var received = (await _repository.GetReceivedAsync(options.From, options.To, cancellationToken))
            .ToDictionary(row => row.EnvelopeId, row => row.AmountCents);

        // The target is the plan in force at the end of the period (the latest % the owner chose).
        var plan = await _envelopeRepository.GetPlanForDateAsync(options.To, cancellationToken);
        var targets = plan?.Items.ToDictionary(item => item.EnvelopeId, item => item.PercentBps) ?? [];

        var envelopes = (await _envelopeRepository.GetAllAsync(cancellationToken))
            .Where(envelope => envelope.IsActive || received.ContainsKey(envelope.Id))
            .OrderBy(envelope => envelope.Role == EnvelopeRole.Replenishment ? 0 : 1)
            .ThenBy(envelope => envelope.SortOrder)
            .ToList();

        var toPercentEnvelopes = envelopes
            .Where(envelope => envelope.Role != EnvelopeRole.Replenishment)
            .Sum(envelope => received.GetValueOrDefault(envelope.Id));

        var rows = envelopes
            .Select(envelope =>
            {
                var amount = received.GetValueOrDefault(envelope.Id);
                var isReplenishment = envelope.Role == EnvelopeRole.Replenishment;
                return new EnvelopeEvaluation(
                    envelope.Id,
                    envelope.Name,
                    envelope.Role,
                    isReplenishment ? null : targets.GetValueOrDefault(envelope.Id),
                    amount,
                    isReplenishment || toPercentEnvelopes <= 0
                        ? null
                        : ProfitFirstEvaluation.Ratio(amount, toPercentEnvelopes));
            })
            .ToList();

        return new ProfitFirstEvaluation(
            options.From,
            options.To,
            (int)totals.ClosedDays,
            totals.CashCollectedCents,
            totals.TransferCollectedCents,
            totals.CostOfSalesCents,
            totals.TheoreticalSalesCents,
            totals.StaffPayCents,
            totals.ShortageCents,
            totals.SurplusCents,
            totals.RecoveredCents,
            await _closingRepository.GetInternalDebtAsync(options.To, cancellationToken),
            rows);
    }
}
