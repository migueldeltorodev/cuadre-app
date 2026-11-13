using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IEnvelopeService
{
    /// <summary>A user's own envelope (role Custom), with no % until a new plan gives it one.</summary>
    Task<Result<Envelope>> CreateAsync(Envelope envelope, CancellationToken cancellationToken = default);

    Task<Result<Envelope>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Envelope>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Rename any envelope; deactivate only a Custom one that is empty and has no % (RF-PF-02).</summary>
    Task<Result<Envelope>> UpdateAsync(EnvelopeChanges changes, CancellationToken cancellationToken = default);

    /// <summary>Envelopes × cash boxes at the end of a day (null = today), with the % in force (RF-PF-06, RF-PF-11).</summary>
    Task<EnvelopeOverview> GetOverviewAsync(DateOnly? date, CancellationToken cancellationToken = default);

    /// <summary>The rows of one envelope with the running balance per box (RF-PF-06).</summary>
    Task<Result<EnvelopeLedger>> GetLedgerAsync(GetEnvelopeLedgerOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Every plan, newest first.</summary>
    Task<IReadOnlyList<AllocationPlan>> GetPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// New % from a date on (RF-PF-03, RF-PF-05). Starting the same day as the latest plan replaces it; an earlier
    /// date is refused, so the % a past day was split with never change.
    /// </summary>
    Task<Result<AllocationPlan>> SavePlanAsync(AllocationPlan plan, CancellationToken cancellationToken = default);
}

public class EnvelopeService : IEnvelopeService
{
    private readonly IEnvelopeRepository _repository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IValidator<Envelope> _validator;
    private readonly IValidator<AllocationPlan> _planValidator;
    private readonly IValidator<GetEnvelopeLedgerOptions> _ledgerValidator;
    private readonly TimeProvider _clock;

    public EnvelopeService(IEnvelopeRepository repository, ICashBoxRepository cashBoxRepository,
        IValidator<Envelope> validator, IValidator<AllocationPlan> planValidator,
        IValidator<GetEnvelopeLedgerOptions> ledgerValidator, TimeProvider clock)
    {
        _repository = repository;
        _cashBoxRepository = cashBoxRepository;
        _validator = validator;
        _planValidator = planValidator;
        _ledgerValidator = ledgerValidator;
        _clock = clock;
    }

    public async Task<Result<Envelope>> CreateAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(envelope, cancellationToken);

        if (envelope.Role != EnvelopeRole.Custom)
        {
            return Result.Fail(new BusinessRuleError("Los sobres de Profit First ya existen: solo se crean sobres propios."));
        }

        if (await _repository.GetByNameAsync(envelope.Name, cancellationToken) is not null)
        {
            return Result.Fail(new ConflictError($"Ya existe un sobre llamado '{envelope.Name}'."));
        }

        var now = _clock.UtcNow();
        envelope.SortOrder = await _repository.GetNextSortOrderAsync(cancellationToken);
        envelope.IsActive = true;
        envelope.CreatedAt = now;
        envelope.UpdatedAt = now;

        await _repository.CreateAsync(envelope, cancellationToken);
        return Result.Ok(envelope);
    }

    public async Task<Result<Envelope>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var envelope = await _repository.GetByIdAsync(id, cancellationToken);
        return envelope is null ? Result.Fail(NotFoundError.For<Envelope>(id)) : Result.Ok(envelope);
    }

    public Task<IEnumerable<Envelope>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Result<Envelope>> UpdateAsync(EnvelopeChanges changes,
        CancellationToken cancellationToken = default)
    {
        var envelope = await _repository.GetByIdAsync(changes.Id, cancellationToken);
        if (envelope is null)
        {
            return Result.Fail(NotFoundError.For<Envelope>(changes.Id));
        }

        var updated = new Envelope
        {
            Id = envelope.Id,
            Name = changes.Name,
            Role = envelope.Role,
            SortOrder = envelope.SortOrder,
            IsActive = changes.IsActive,
            CreatedAt = envelope.CreatedAt
        };
        await _validator.ValidateOrThrowAsync(updated, cancellationToken);

        var sameName = await _repository.GetByNameAsync(changes.Name, cancellationToken);
        if (sameName is not null && sameName.Id != envelope.Id)
        {
            return Result.Fail(new ConflictError($"Ya existe un sobre llamado '{changes.Name}'."));
        }

        if (envelope.IsActive && !changes.IsActive)
        {
            var refusal = await CheckCanDeactivateAsync(envelope, cancellationToken);
            if (refusal.IsFailed)
            {
                return refusal;
            }
        }

        updated.UpdatedAt = _clock.UtcNow();
        await _repository.UpdateAsync(updated, cancellationToken);
        return Result.Ok(updated);
    }

    public async Task<EnvelopeOverview> GetOverviewAsync(DateOnly? date, CancellationToken cancellationToken = default)
    {
        var day = date ?? _clock.Today();
        var envelopes = (await _repository.GetAllAsync(cancellationToken)).ToList();
        var boxes = (await _cashBoxRepository.GetAllAsync(cancellationToken)).ToList();
        var balances = await _repository.GetBalancesAsync(day, cancellationToken: cancellationToken);
        var plan = await _repository.GetPlanForDateAsync(day, cancellationToken);

        // Inactive envelopes and boxes only show up while they still hold something (money must never hide).
        var columns = boxes
            .Select(box => new EnvelopeBoxColumn(box.Id, box.Name, box.Currency, box.Kind, box.IsActive,
                balances.Where(b => b.CashBoxId == box.Id).Sum(b => b.AmountCents)))
            .Where(column => column.IsActive || balances.Any(b => b.CashBoxId == column.Id))
            .ToList();

        var rows = envelopes
            .Where(envelope => envelope.IsActive || balances.Any(b => b.EnvelopeId == envelope.Id))
            .Select(envelope => new EnvelopeRow(
                envelope,
                envelope.TakesPercent
                    ? plan?.Items.FirstOrDefault(item => item.EnvelopeId == envelope.Id)?.PercentBps ?? 0
                    : null,
                balances.Where(b => b.EnvelopeId == envelope.Id).ToList()))
            .ToList();

        return new EnvelopeOverview(day, columns, rows, plan);
    }

    public async Task<Result<EnvelopeLedger>> GetLedgerAsync(GetEnvelopeLedgerOptions options,
        CancellationToken cancellationToken = default)
    {
        var envelope = await _repository.GetByIdAsync(options.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Fail(NotFoundError.For<Envelope>(options.EnvelopeId));
        }

        await _ledgerValidator.ValidateOrThrowAsync(options, cancellationToken);

        IReadOnlyList<EnvelopeBoxBalance> opening = options.From is { } from
            ? await _repository.GetBalancesAsync(from.AddDays(-1), envelope.Id, cancellationToken)
            : [];
        var closing = await _repository.GetBalancesAsync(options.To, envelope.Id, cancellationToken);

        if (options.CashBoxId is { } cashBoxId)
        {
            opening = opening.Where(balance => balance.CashBoxId == cashBoxId).ToList();
            closing = closing.Where(balance => balance.CashBoxId == cashBoxId).ToList();
        }

        var movements = await _repository.GetMovementsAsync(options, cancellationToken);
        return Result.Ok(new EnvelopeLedger(envelope, opening, EnvelopeMath.RunningBalances(opening, movements),
            closing));
    }

    public Task<IReadOnlyList<AllocationPlan>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetPlansAsync(cancellationToken);
    }

    public async Task<Result<AllocationPlan>> SavePlanAsync(AllocationPlan plan,
        CancellationToken cancellationToken = default)
    {
        await _planValidator.ValidateOrThrowAsync(plan, cancellationToken);

        var envelopes = (await _repository.GetAllAsync(cancellationToken)).ToDictionary(envelope => envelope.Id);
        foreach (var item in plan.Items)
        {
            if (!envelopes.TryGetValue(item.EnvelopeId, out var envelope) || !envelope.IsActive)
            {
                return Result.Fail(new BusinessRuleError("Uno de los sobres no existe o está desactivado."));
            }

            if (!envelope.TakesPercent)
            {
                return Result.Fail(new BusinessRuleError(
                    $"'{envelope.Name}' no lleva %: recibe el costo de lo vendido (A2) en cada cierre."));
            }
        }

        var latest = await _repository.GetLatestPlanAsync(cancellationToken);
        if (latest is not null && plan.EffectiveFrom < latest.EffectiveFrom)
        {
            return Result.Fail(new BusinessRuleError(
                $"Ya hay un reparto que empieza el {latest.EffectiveFrom:dd/MM/yyyy}. El nuevo tiene que empezar ese día o después."));
        }

        var replaceExisting = latest is not null && plan.EffectiveFrom == latest.EffectiveFrom;
        var now = _clock.UtcNow();

        // Every active envelope that takes a % gets a row, 0 included: the plan reads complete on its own.
        var items = envelopes.Values
            .Where(envelope => envelope.IsActive && envelope.TakesPercent)
            .OrderBy(envelope => envelope.SortOrder)
            .Select(envelope => new AllocationPlanItem
            {
                EnvelopeId = envelope.Id,
                PercentBps = plan.Items.FirstOrDefault(item => item.EnvelopeId == envelope.Id)?.PercentBps ?? 0,
                EnvelopeName = envelope.Name
            })
            .ToList();

        var toSave = new AllocationPlan
        {
            Id = replaceExisting ? latest!.Id : plan.Id,
            EffectiveFrom = plan.EffectiveFrom,
            Notes = plan.Notes,
            CreatedAt = replaceExisting ? latest!.CreatedAt : now,
            UpdatedAt = now,
            Items = items
        };

        await _repository.SavePlanAsync(toSave, replaceExisting, cancellationToken);
        return Result.Ok((await _repository.GetPlansAsync(cancellationToken)).First(p => p.Id == toSave.Id));
    }

    private async Task<Result> CheckCanDeactivateAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.Role != EnvelopeRole.Custom)
        {
            return Result.Fail(new BusinessRuleError(
                $"'{envelope.Name}' es uno de los sobres de Profit First: se puede renombrar, pero no desactivar."));
        }

        if (await _repository.HasBalanceAsync(envelope.Id, cancellationToken))
        {
            return Result.Fail(new BusinessRuleError(
                $"'{envelope.Name}' todavía tiene plata. Pasala a otro sobre antes de desactivarlo."));
        }

        var latest = await _repository.GetLatestPlanAsync(cancellationToken);
        if (latest?.Items.Any(item => item.EnvelopeId == envelope.Id && item.PercentBps > 0) == true)
        {
            return Result.Fail(new BusinessRuleError(
                $"'{envelope.Name}' recibe un % del reparto. Primero dejalo en 0 en un reparto nuevo."));
        }

        return Result.Ok();
    }
}
