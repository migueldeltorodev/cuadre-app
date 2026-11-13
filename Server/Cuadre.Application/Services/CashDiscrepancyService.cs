using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>
/// RF-CIE-12/13, RN-06c: the shortages are claimed. Recovered money comes in the day it is recovered and is split
/// like the A3 of that day (by the plan % of that date, among the envelopes with a %). What is never recovered is
/// written off as lost. Surpluses are only listed.
/// </summary>
public interface ICashDiscrepancyService
{
    Task<IEnumerable<CashDiscrepancy>> GetAllAsync(GetAllCashDiscrepanciesOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllCashDiscrepanciesOptions options, CancellationToken cancellationToken = default);

    Task<Result<CashDiscrepancy>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(int Count, long PendingCents)> GetPendingTotalsAsync(CancellationToken cancellationToken = default);

    Task<Result<CashDiscrepancy>> RecoverAsync(RecoverShortage request, CancellationToken cancellationToken = default);

    /// <summary>The reversals are dated TODAY (a recovery of a closed day stays in that day's books).</summary>
    Task<Result<CashDiscrepancy>> VoidRecoveryAsync(VoidShortageRecovery request,
        CancellationToken cancellationToken = default);

    Task<Result<CashDiscrepancy>> WriteOffAsync(WriteOffShortage request,
        CancellationToken cancellationToken = default);
}

public class CashDiscrepancyService : ICashDiscrepancyService
{
    private readonly ICashDiscrepancyRepository _repository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IClosedDayGuard _closedDayGuard;
    private readonly IValidator<RecoverShortage> _recoverValidator;
    private readonly IValidator<WriteOffShortage> _writeOffValidator;
    private readonly IValidator<VoidShortageRecovery> _voidValidator;
    private readonly IValidator<GetAllCashDiscrepanciesOptions> _optionsValidator;
    private readonly TimeProvider _clock;

    public CashDiscrepancyService(ICashDiscrepancyRepository repository, IEnvelopeRepository envelopeRepository,
        ICashBoxRepository cashBoxRepository, IClosedDayGuard closedDayGuard,
        IValidator<RecoverShortage> recoverValidator, IValidator<WriteOffShortage> writeOffValidator,
        IValidator<VoidShortageRecovery> voidValidator, IValidator<GetAllCashDiscrepanciesOptions> optionsValidator,
        TimeProvider clock)
    {
        _repository = repository;
        _envelopeRepository = envelopeRepository;
        _cashBoxRepository = cashBoxRepository;
        _closedDayGuard = closedDayGuard;
        _recoverValidator = recoverValidator;
        _writeOffValidator = writeOffValidator;
        _voidValidator = voidValidator;
        _optionsValidator = optionsValidator;
        _clock = clock;
    }

    public async Task<IEnumerable<CashDiscrepancy>> GetAllAsync(GetAllCashDiscrepanciesOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllCashDiscrepanciesOptions options,
        CancellationToken cancellationToken = default) =>
        _repository.GetCountAsync(options, cancellationToken);

    public async Task<Result<CashDiscrepancy>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var discrepancy = await _repository.GetByIdAsync(id, cancellationToken);
        return discrepancy is null ? Result.Fail(NotFoundError.For<CashDiscrepancy>(id)) : Result.Ok(discrepancy);
    }

    public Task<(int Count, long PendingCents)> GetPendingTotalsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetPendingTotalsAsync(cancellationToken);

    public async Task<Result<CashDiscrepancy>> RecoverAsync(RecoverShortage request,
        CancellationToken cancellationToken = default)
    {
        var discrepancy = await _repository.GetByIdAsync(request.DiscrepancyId, cancellationToken);
        if (discrepancy is null)
        {
            return Result.Fail(NotFoundError.For<CashDiscrepancy>(request.DiscrepancyId));
        }

        await _recoverValidator.ValidateOrThrowAsync(request, cancellationToken);

        var pending = EnsurePendingShortage(discrepancy);
        if (pending.IsFailed)
        {
            return pending;
        }

        if (request.AmountCents > discrepancy.PendingCents)
        {
            return Result.Fail(new BusinessRuleError(
                $"Del faltante quedan {Money(discrepancy.PendingCents)} por recuperar: no se puede recuperar más."));
        }

        var today = _clock.Today();
        var date = request.Date ?? today;
        if (date > today)
        {
            return Result.Fail(new BusinessRuleError("La recuperación no puede tener fecha futura."));
        }

        if (date < discrepancy.Date)
        {
            return Result.Fail(new BusinessRuleError("La recuperación no puede ser anterior al día del faltante."));
        }

        var open = await _closedDayGuard.EnsureOpenAsync(date, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var cashBox = await _cashBoxRepository.GetByIdAsync(request.CashBoxId, cancellationToken);
        if (cashBox is null || !cashBox.IsActive || cashBox.Currency != Currency.CUP)
        {
            return Result.Fail(new BusinessRuleError("La caja elegida no existe, está desactivada o no es en CUP."));
        }

        // Split like the A3 of that day: the plan in force then, among the envelopes with a % (D-35).
        var envelopes = (await _envelopeRepository.GetAllAsync(cancellationToken)).ToDictionary(e => e.Id);
        var plan = await _envelopeRepository.GetPlanForDateAsync(date, cancellationToken)
                   ?? throw new InvalidOperationException("No allocation plan: the seeder did not run.");
        var items = plan.Items
            .Where(item => envelopes.ContainsKey(item.EnvelopeId))
            .OrderBy(item => envelopes[item.EnvelopeId].SortOrder)
            .ToList();
        var shares = ClosingCalculator.Apportion(request.AmountCents, items.Select(i => i.PercentBps).ToList());

        var now = _clock.UtcNow();
        var recovery = new ShortageRecovery
        {
            Id = Guid.NewGuid(),
            DiscrepancyId = discrepancy.Id,
            Date = date,
            CashBoxId = cashBox.Id,
            AmountCents = request.AmountCents,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = ShortageRecoveryStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now
        };

        var description = $"Recuperación del faltante del {ClosedDayGuard.Format(discrepancy.Date)}";
        var movements = items
            .Select((item, index) => (item.EnvelopeId, Amount: shares[index]))
            .Where(share => share.Amount != 0)
            .Select(share => new CashMovement
            {
                Id = Guid.NewGuid(),
                CashBoxId = cashBox.Id,
                EnvelopeId = share.EnvelopeId,
                Date = date,
                Kind = CashMovementKind.ShortageRecovery,
                AmountCents = share.Amount,
                Description = description,
                RecoveryId = recovery.Id,
                CreatedAt = now
            })
            .ToList();

        if (request.AmountCents == discrepancy.PendingCents)
        {
            discrepancy.Status = CashDiscrepancyStatus.Recovered;
            discrepancy.ResolvedAt = now;
        }

        discrepancy.UpdatedAt = now;
        await _repository.CreateRecoveryAsync(recovery, movements, discrepancy, cancellationToken);
        return await ReloadAsync(discrepancy.Id, cancellationToken);
    }

    public async Task<Result<CashDiscrepancy>> VoidRecoveryAsync(VoidShortageRecovery request,
        CancellationToken cancellationToken = default)
    {
        var recovery = await _repository.GetRecoveryByIdAsync(request.RecoveryId, cancellationToken);
        if (recovery is null)
        {
            return Result.Fail(NotFoundError.For<ShortageRecovery>(request.RecoveryId));
        }

        await _voidValidator.ValidateOrThrowAsync(request, cancellationToken);

        if (recovery.Status == ShortageRecoveryStatus.Voided)
        {
            return Result.Fail(new BusinessRuleError("Esa recuperación ya está anulada."));
        }

        var today = _clock.Today();
        var open = await _closedDayGuard.EnsureOpenAsync(today, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var discrepancy = await _repository.GetByIdAsync(recovery.DiscrepancyId, cancellationToken)
                          ?? throw new InvalidOperationException("A recovery without its shortage.");
        if (discrepancy.Status is not (CashDiscrepancyStatus.Pending or CashDiscrepancyStatus.Recovered))
        {
            return Result.Fail(new BusinessRuleError(
                "El faltante ya se dio por perdido o su cierre se reabrió: sus recuperaciones no se tocan."));
        }

        var now = _clock.UtcNow();
        recovery.Status = ShortageRecoveryStatus.Voided;
        recovery.VoidReason = request.Reason.Trim();
        recovery.VoidedAt = now;
        recovery.UpdatedAt = now;

        var description = $"Anulación de la recuperación del faltante del {ClosedDayGuard.Format(discrepancy.Date)}";
        var reversals = (await _repository.GetRecoveryMovementsAsync(recovery.Id, cancellationToken))
            .Select(movement => new CashMovement
            {
                Id = Guid.NewGuid(),
                CashBoxId = movement.CashBoxId,
                EnvelopeId = movement.EnvelopeId,
                Date = today,
                Kind = CashMovementKind.ShortageRecoveryVoid,
                AmountCents = -movement.AmountCents,
                Description = description,
                RecoveryId = recovery.Id,
                CreatedAt = now
            })
            .ToList();

        discrepancy.Status = CashDiscrepancyStatus.Pending;
        discrepancy.ResolvedAt = null;
        discrepancy.UpdatedAt = now;
        await _repository.VoidRecoveryAsync(recovery, reversals, discrepancy, cancellationToken);
        return await ReloadAsync(discrepancy.Id, cancellationToken);
    }

    public async Task<Result<CashDiscrepancy>> WriteOffAsync(WriteOffShortage request,
        CancellationToken cancellationToken = default)
    {
        var discrepancy = await _repository.GetByIdAsync(request.DiscrepancyId, cancellationToken);
        if (discrepancy is null)
        {
            return Result.Fail(NotFoundError.For<CashDiscrepancy>(request.DiscrepancyId));
        }

        await _writeOffValidator.ValidateOrThrowAsync(request, cancellationToken);

        var pending = EnsurePendingShortage(discrepancy);
        if (pending.IsFailed)
        {
            return pending;
        }

        // No money moves: the shortage already lowered A1 the day it happened. Only its status changes.
        var now = _clock.UtcNow();
        discrepancy.Status = CashDiscrepancyStatus.Lost;
        discrepancy.ResolutionNote = request.Note.Trim();
        discrepancy.ResolvedAt = now;
        discrepancy.UpdatedAt = now;
        await _repository.UpdateResolutionAsync(discrepancy, cancellationToken);
        return await ReloadAsync(discrepancy.Id, cancellationToken);
    }

    private static Result EnsurePendingShortage(CashDiscrepancy discrepancy)
    {
        if (discrepancy.Kind != CashDiscrepancyKind.Shortage)
        {
            return Result.Fail(new BusinessRuleError("Un sobrante solo se registra: no se recupera ni se pierde."));
        }

        return discrepancy.Status switch
        {
            CashDiscrepancyStatus.Pending => Result.Ok(),
            CashDiscrepancyStatus.Recovered => Result.Fail(new BusinessRuleError("Ese faltante ya se recuperó entero.")),
            CashDiscrepancyStatus.Lost => Result.Fail(new BusinessRuleError("Ese faltante ya se dio por perdido.")),
            _ => Result.Fail(new BusinessRuleError("El cierre de ese faltante se reabrió: ya no cuenta."))
        };
    }

    private async Task<Result<CashDiscrepancy>> ReloadAsync(Guid id, CancellationToken cancellationToken) =>
        Result.Ok(await _repository.GetByIdAsync(id, cancellationToken)
                  ?? throw new InvalidOperationException("The shortage was not found right after saving it."));

    private static string Money(long cents) =>
        (cents / 100m).ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture) + " CUP";
}
