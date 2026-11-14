using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IDailyClosingService
{
    /// <summary>The "planilla" and everything the closing screens need for that day (default: today).</summary>
    Task<DailyClosingDraft> GetDraftAsync(DateOnly? date, CancellationToken cancellationToken = default);

    /// <summary>Computes the whole closing (sales, A1/A2/A3, pay, split, debt) WITHOUT saving it.</summary>
    Task<Result<DailyClosing>> PreviewAsync(DailyClosingInput input, CancellationToken cancellationToken = default);

    /// <summary>Computes it again, checks the note (RF-CIE-04) and saves it with its movements.</summary>
    Task<Result<DailyClosing>> CreateAsync(DailyClosingInput input, CancellationToken cancellationToken = default);

    Task<Result<DailyClosing>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<DailyClosingSummary>> GetAllAsync(GetAllDailyClosingsOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllDailyClosingsOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// RF-CIE-10: only the LATEST closed day can be reopened, with a reason. Its movements are reversed on the same
    /// day, so the day is open again exactly as it was before closing.
    /// </summary>
    Task<Result<DailyClosing>> ReopenAsync(ReopenDailyClosing request, CancellationToken cancellationToken = default);

    /// <summary>RF-CIE-09: business days already over that are still not closed.</summary>
    Task<PendingClosingDays> GetPendingDaysAsync(CancellationToken cancellationToken = default);
}

public class DailyClosingService : IDailyClosingService
{
    private readonly IDailyClosingRepository _repository;
    private readonly IStaffRepository _staffRepository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly IBusinessSettingsRepository _settingsRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly ICashDiscrepancyRepository _discrepancyRepository;
    private readonly IValidator<DailyClosingInput> _validator;
    private readonly IValidator<ReopenDailyClosing> _reopenValidator;
    private readonly IValidator<GetAllDailyClosingsOptions> _optionsValidator;
    private readonly TimeProvider _clock;

    public DailyClosingService(IDailyClosingRepository repository, IStaffRepository staffRepository,
        IEnvelopeRepository envelopeRepository, ICashBoxRepository cashBoxRepository,
        IBusinessSettingsRepository settingsRepository, IExchangeRateRepository exchangeRateRepository,
        ICashDiscrepancyRepository discrepancyRepository, IValidator<DailyClosingInput> validator, IValidator<ReopenDailyClosing> reopenValidator,
        IValidator<GetAllDailyClosingsOptions> optionsValidator, TimeProvider clock)
    {
        _repository = repository;
        _staffRepository = staffRepository;
        _envelopeRepository = envelopeRepository;
        _cashBoxRepository = cashBoxRepository;
        _settingsRepository = settingsRepository;
        _exchangeRateRepository = exchangeRateRepository;
        _discrepancyRepository = discrepancyRepository;
        _validator = validator;
        _reopenValidator = reopenValidator;
        _optionsValidator = optionsValidator;
        _clock = clock;
    }

    public async Task<DailyClosingDraft> GetDraftAsync(DateOnly? date, CancellationToken cancellationToken = default)
    {
        var day = date ?? _clock.Today();
        var settings = await _settingsRepository.GetAsync(cancellationToken)
                       ?? throw new InvalidOperationException("The settings row is missing: the seeder did not run.");
        var lastClosed = await _repository.GetLastClosedDateAsync(cancellationToken);
        var existing = await _repository.GetConfirmedIdAsync(day, cancellationToken);
        var scheme = await _staffRepository.GetPaySchemeForDateAsync(day, cancellationToken)
                     ?? throw new InvalidOperationException("No staff pay scheme: the seeder did not run.");

        string? problem = null;
        if (day > _clock.Today())
        {
            problem = "No se puede cerrar un día que todavía no terminó.";
        }
        else if (existing is not null)
        {
            problem = $"El {ClosedDayGuard.Format(day)} ya está cerrado.";
        }
        else if (lastClosed is { } closed && day < closed)
        {
            problem = $"Ya está cerrado el {ClosedDayGuard.Format(closed)}: solo se pueden cerrar días posteriores.";
        }

        return new DailyClosingDraft(
            day,
            settings.ClosedDays.Contains(day.DayOfWeek),
            lastClosed,
            existing,
            await _exchangeRateRepository.GetByDateAsync(day, cancellationToken) is null,
            problem,
            await _repository.GetCounterLinesAsync(day, cancellationToken),
            await _repository.GetPaymentMethodsAsync(cancellationToken),
            scheme,
            await _repository.GetInternalDebtAsync(day.AddDays(-1), cancellationToken),
            settings.CashDifferenceThresholdCents);
    }

    public async Task<Result<DailyClosing>> PreviewAsync(DailyClosingInput input,
        CancellationToken cancellationToken = default)
    {
        var built = await BuildAsync(input, cancellationToken);
        if (built.IsFailed)
        {
            return built.ToResult();
        }

        return Result.Ok(built.Value.Closing);
    }

    public async Task<Result<DailyClosing>> CreateAsync(DailyClosingInput input,
        CancellationToken cancellationToken = default)
    {
        var built = await BuildAsync(input, cancellationToken);
        if (built.IsFailed)
        {
            return built.ToResult();
        }

        var (closing, stockMovements, cashMovements) = built.Value;
        if (closing.RequiresNote && string.IsNullOrWhiteSpace(closing.Notes))
        {
            return Result.Fail(new BusinessRuleError(
                "La diferencia de caja supera el umbral: escribí una nota que explique qué pasó."));
        }

        await _repository.CreateAsync(closing, stockMovements, cashMovements, closing.CreatedAt, cancellationToken);
        var saved = await _repository.GetByIdAsync(closing.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The closing was not found right after saving it.");
        saved.PossibleDuplicateOf = closing.PossibleDuplicateOf;
        return Result.Ok(saved);
    }

    public async Task<Result<DailyClosing>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var closing = await _repository.GetByIdAsync(id, cancellationToken);
        return closing is null ? Result.Fail(NotFoundError.For<DailyClosing>(id)) : Result.Ok(closing);
    }

    public async Task<IEnumerable<DailyClosingSummary>> GetAllAsync(GetAllDailyClosingsOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllDailyClosingsOptions options, CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }

    public async Task<Result<DailyClosing>> ReopenAsync(ReopenDailyClosing request,
        CancellationToken cancellationToken = default)
    {
        var closing = await _repository.GetByIdAsync(request.ClosingId, cancellationToken);
        if (closing is null)
        {
            return Result.Fail(NotFoundError.For<DailyClosing>(request.ClosingId));
        }

        await _reopenValidator.ValidateOrThrowAsync(request, cancellationToken);

        if (closing.Status == DailyClosingStatus.Voided)
        {
            return Result.Fail(new BusinessRuleError($"El cierre del {ClosedDayGuard.Format(closing.Date)} ya fue reabierto."));
        }

        var lastClosed = await _repository.GetLastClosedDateAsync(cancellationToken);
        if (lastClosed != closing.Date)
        {
            return Result.Fail(new BusinessRuleError(
                $"Solo se puede reabrir el último día cerrado (el {ClosedDayGuard.Format(lastClosed!.Value)}). " +
                "Reabrí primero los días posteriores."));
        }

        // A recovery came in on a later day and already moved money (D-36): undo it first.
        if (await _discrepancyRepository.HasRecoveriesAsync(closing.Id, cancellationToken))
        {
            return Result.Fail(new BusinessRuleError(
                "El faltante de este cierre ya tiene recuperaciones: anulalas antes de reabrirlo."));
        }

        var now = _clock.UtcNow();
        closing.Status = DailyClosingStatus.Voided;
        closing.VoidReason = request.Reason.Trim();
        closing.VoidedAt = now;
        closing.UpdatedAt = now;

        var description = $"Reapertura del cierre del {ClosedDayGuard.Format(closing.Date)}";

        // Same day as the closing (D-27): the day then sums exactly as before it was closed. No costs on the
        // reversals: the rows of a reopened closing are left out of the cost replay anyway.
        var stockReversals = (await _repository.GetStockMovementsAsync(closing.Id, cancellationToken))
            .Select(movement => new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = movement.ProductId,
                LocationId = movement.LocationId,
                Date = closing.Date,
                Kind = StockMovementKind.ClosingVoid,
                QuantityMilli = -movement.QuantityMilli,
                ClosingId = closing.Id,
                CreatedAt = now
            })
            .ToList();

        var cashReversals = (await _repository.GetCashMovementsAsync(closing.Id, cancellationToken))
            .Select(movement => new CashMovement
            {
                Id = Guid.NewGuid(),
                CashBoxId = movement.CashBoxId,
                EnvelopeId = movement.EnvelopeId,
                Date = closing.Date,
                Kind = CashMovementKind.ClosingVoid,
                AmountCents = -movement.AmountCents,
                Description = description,
                ClosingId = closing.Id,
                CreatedAt = now
            })
            .ToList();

        await _repository.VoidAsync(closing, stockReversals, cashReversals, now, cancellationToken);
        return Result.Ok(await _repository.GetByIdAsync(closing.Id, cancellationToken) ?? closing);
    }

    public async Task<PendingClosingDays> GetPendingDaysAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken)
                       ?? throw new InvalidOperationException("The settings row is missing: the seeder did not run.");
        var lastClosed = await _repository.GetLastClosedDateAsync(cancellationToken);
        var firstActivity = lastClosed is null
            ? await _repository.GetFirstActivityDateAsync(cancellationToken)
            : null;

        var days = ClosingCalendar.PendingDays(lastClosed, firstActivity, _clock.Today(), settings.ClosedDays);
        return new PendingClosingDays(lastClosed, days.Count, days.Take(ClosingCalendar.MaxListed).ToList());
    }

    /// <summary>
    /// The whole closing, from what the user typed plus what the ledgers say. Every rule that needs data lives here;
    /// the validator only checks the shape. The note is checked by Create only (the preview tells if it is needed).
    /// </summary>
    private async Task<Result<(DailyClosing Closing, List<StockMovement> Stock, List<CashMovement> Cash)>> BuildAsync(
        DailyClosingInput input, CancellationToken cancellationToken)
    {
        await _validator.ValidateOrThrowAsync(input, cancellationToken);

        var draft = await GetDraftAsync(input.Date, cancellationToken);
        if (draft.Problem is { } problem)
        {
            return Result.Fail(new BusinessRuleError(problem));
        }

        // ---- Counter count (RN-05, RN-06, RN-07) ----
        var counts = input.Counts.ToDictionary(count => (count.ProductId, count.LocationId));
        if (counts.Keys.Any(key => !draft.Lines.Any(line => line.ProductId == key.ProductId &&
                                                            line.LocationId == key.LocationId)))
        {
            return Result.Fail(new BusinessRuleError(
                "Uno de los productos contados no estaba en el mostrador ese día."));
        }

        var closingId = Guid.NewGuid();
        var lines = new List<DailyClosingLine>();
        foreach (var counter in draft.Lines)
        {
            if (!counts.TryGetValue((counter.ProductId, counter.LocationId), out var count))
            {
                return Result.Fail(new BusinessRuleError($"Falta contar {counter.Name} ({counter.LocationName})."));
            }

            if (!counter.AllowsDecimals && count.CountedMilli % Units.MilliPerUnit != 0)
            {
                return Result.Fail(new BusinessRuleError($"{counter.Name} se cuenta en unidades enteras."));
            }

            var result = ClosingCalculator.Count(new CountInput(counter.OpeningMilli, counter.InMilli,
                counter.OutMilli, count.CountedMilli, counter.UnitPriceCents, counter.ToleranceBps,
                counter.LastCostCupMicros));

            if (result.SoldMilli != 0 && counter.UnitPriceCents is null)
            {
                return Result.Fail(new BusinessRuleError(
                    $"{counter.Name} se vendió pero no tiene precio de venta: ponéselo antes de cerrar."));
            }

            lines.Add(new DailyClosingLine
            {
                Id = Guid.NewGuid(),
                ClosingId = closingId,
                LineNo = lines.Count + 1,
                ProductId = counter.ProductId,
                ProductCode = counter.Code,
                ProductName = counter.Name,
                LocationId = counter.LocationId,
                LocationName = counter.LocationName,
                StockUnit = counter.StockUnit,
                OpeningMilli = counter.OpeningMilli,
                InMilli = counter.InMilli,
                OutMilli = counter.OutMilli,
                ExpectedMilli = result.ExpectedMilli,
                CountedMilli = count.CountedMilli,
                SoldMilli = result.SoldMilli,
                UnitPriceCents = counter.UnitPriceCents,
                SalesCents = result.SalesCents,
                ToleranceBps = counter.ToleranceBps,
                ToleranceCents = result.ToleranceCents,
                UnitCostUsdMicros = counter.LastCostUsdMicros,
                UnitCostCupMicros = counter.LastCostCupMicros,
                CostCents = result.CostCents
            });
        }

        // ---- Collections (RF-CAJ-02): methods not sent count as 0 ----
        var methods = draft.PaymentMethods.ToDictionary(method => method.Id);
        if (input.Collections.Any(collection => !methods.ContainsKey(collection.PaymentMethodId)))
        {
            return Result.Fail(new BusinessRuleError(
                "Una de las formas de cobro no existe, está desactivada o no cobra en CUP."));
        }

        var collections = input.Collections
            .Where(collection => collection.AmountCents > 0)
            .Select(collection =>
            {
                var method = methods[collection.PaymentMethodId];
                return new DailyClosingCollection
                {
                    ClosingId = closingId,
                    PaymentMethodId = method.Id,
                    PaymentMethodName = method.Name,
                    CashBoxId = method.CashBoxId,
                    CashBoxName = method.CashBoxName,
                    IsTransfer = method.IsTransfer,
                    AmountCents = collection.AmountCents
                };
            })
            .ToList();

        // ---- Who worked (RF-CIE-14) ----
        Employee? employee = null;
        if (input.EmployeeId is { } employeeId)
        {
            employee = await _staffRepository.GetEmployeeByIdAsync(employeeId, cancellationToken);
            if (employee is null || !employee.IsActive)
            {
                return Result.Fail(new BusinessRuleError("La persona elegida no existe o está desactivada."));
            }
        }

        // ---- A1, A2, A3, difference, pay, split, debt ----
        var totals = ClosingCalculator.Totals(
            lines.Select(line => new CountResult(line.ExpectedMilli, line.SoldMilli, line.SalesCents,
                line.ToleranceCents, line.CostCents)),
            collections.Where(c => !c.IsTransfer).Sum(c => c.AmountCents),
            collections.Where(c => c.IsTransfer).Sum(c => c.AmountCents),
            draft.PayScheme.DailyFixedCents,
            draft.PayScheme.PercentOfSalesBps);
        var split = ClosingCalculator.Split(totals, draft.DebtBeforeCents);

        var envelopes = (await _envelopeRepository.GetAllAsync(cancellationToken)).ToDictionary(e => e.Id);
        var plan = await _envelopeRepository.GetPlanForDateAsync(input.Date, cancellationToken)
                   ?? throw new InvalidOperationException("No allocation plan: the seeder did not run.");
        var replenishment = envelopes.Values.Single(e => e.Role == EnvelopeRole.Replenishment);
        var operations = envelopes.Values.Single(e => e.Role == EnvelopeRole.Operations);

        var items = plan.Items
            .Where(item => envelopes.ContainsKey(item.EnvelopeId))
            .OrderBy(item => envelopes[item.EnvelopeId].SortOrder)
            .ToList();
        var shares = ClosingCalculator.Apportion(split.ToOthersCents, items.Select(i => i.PercentBps).ToList());

        var now = _clock.UtcNow();
        var cashCollected = totals.CashCollectedCents;
        var allocations = new List<DailyClosingAllocation>
        {
            new()
            {
                ClosingId = closingId,
                EnvelopeId = replenishment.Id,
                EnvelopeName = replenishment.Name,
                Role = replenishment.Role,
                SortOrder = replenishment.SortOrder,
                PercentBps = null,
                AmountCents = totals.CollectedCents - split.ToOthersCents,
                CashCents = cashCollected - split.ToOthersCents,
                BankCents = totals.TransferCollectedCents
            }
        };
        allocations.AddRange(items.Select((item, index) => new DailyClosingAllocation
        {
            ClosingId = closingId,
            EnvelopeId = item.EnvelopeId,
            EnvelopeName = envelopes[item.EnvelopeId].Name,
            Role = envelopes[item.EnvelopeId].Role,
            SortOrder = envelopes[item.EnvelopeId].SortOrder,
            PercentBps = item.PercentBps,
            AmountCents = shares[index],
            CashCents = shares[index] - (item.EnvelopeId == operations.Id ? totals.StaffPayCents : 0),
            BankCents = 0
        }));

        // ---- Cash movements: collections into Reposición, then the split, then the staff pay ----
        var shareList = items.Select((item, index) => (item.EnvelopeId, shares[index])).ToList();
        var needsCashBox = totals.StaffPayCents > 0 || shareList.Any(share => share.Item2 != 0);
        var primaryBox = (await _cashBoxRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(box => box.IsActive && box.Currency == Currency.CUP && box.Kind == CashBoxKind.Cash);
        if (needsCashBox && primaryBox is null)
        {
            return Result.Fail(new BusinessRuleError(
                "Hace falta una caja de efectivo en CUP activa: de ahí salen el reparto y el pago al dependiente."));
        }

        var dayText = ClosedDayGuard.Format(input.Date);
        var cashMovements = collections
            .Select(collection => NewCash(collection.CashBoxId, replenishment.Id, CashMovementKind.Collection,
                collection.AmountCents, $"Cobro del {dayText}: {collection.PaymentMethodName}"))
            .ToList();

        if (needsCashBox)
        {
            var cashByBox = collections
                .Where(collection => !collection.IsTransfer)
                .GroupBy(collection => collection.CashBoxId)
                .Select(group => (group.Key, group.Sum(c => c.AmountCents)))
                .ToList();

            foreach (var share in ClosingCalculator.AssignBoxes(cashByBox, primaryBox!.Id, shareList))
            {
                var text = $"Reparto del {dayText}: {envelopes[share.EnvelopeId].Name}";
                cashMovements.Add(NewCash(share.CashBoxId, replenishment.Id, CashMovementKind.Allocation,
                    -share.AmountCents, text));
                cashMovements.Add(NewCash(share.CashBoxId, share.EnvelopeId, CashMovementKind.Allocation,
                    share.AmountCents, text));
            }

            if (totals.StaffPayCents > 0)
            {
                var who = employee is null ? "" : $" ({employee.Name})";
                cashMovements.Add(NewCash(primaryBox.Id, operations.Id, CashMovementKind.StaffPay,
                    -totals.StaffPayCents, $"Pago al dependiente del {dayText}{who}"));
            }
        }

        // ---- Stock: what left the counter as sales. Only outflows carry the cost (an entry with cost would be
        // taken for a purchase by the cost replay). ----
        var stockMovements = lines
            .Where(line => line.SoldMilli != 0)
            .Select(line => new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = line.ProductId,
                LocationId = line.LocationId,
                Date = input.Date,
                Kind = StockMovementKind.Sale,
                QuantityMilli = -line.SoldMilli,
                UnitCostUsdMicros = line.SoldMilli > 0 ? line.UnitCostUsdMicros : null,
                UnitCostCupMicros = line.SoldMilli > 0 ? line.UnitCostCupMicros : null,
                ClosingId = closingId,
                CreatedAt = now
            })
            .ToList();

        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        var closing = new DailyClosing
        {
            Id = closingId,
            Date = input.Date,
            Status = DailyClosingStatus.Confirmed,
            EmployeeId = employee?.Id,
            EmployeeName = employee?.Name,
            Notes = notes,
            TheoreticalSalesCents = totals.TheoreticalSalesCents,
            ShrinkageToleranceCents = totals.ToleranceCents,
            CashCollectedCents = totals.CashCollectedCents,
            TransferCollectedCents = totals.TransferCollectedCents,
            CostOfSalesCents = totals.CostOfSalesCents,
            CashDifferenceCents = totals.CashDifferenceCents,
            AdjustedDifferenceCents = totals.AdjustedDifferenceCents,
            ThresholdCents = draft.ThresholdCents,
            StaffPayCents = totals.StaffPayCents,
            PayFixedCents = draft.PayScheme.DailyFixedCents,
            PayPercentBps = draft.PayScheme.PercentOfSalesBps,
            DebtBeforeCents = draft.DebtBeforeCents,
            DebtCreatedCents = split.DebtCreatedCents,
            DebtRepaidCents = split.DebtRepaidCents,
            DebtAfterCents = split.DebtAfterCents,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = lines,
            Collections = collections,
            Allocations = allocations
        };

        // RF-CIE-11: a warning only. Days without sales would all look alike.
        if (totals.TheoreticalSalesCents > 0 || totals.CollectedCents > 0)
        {
            closing.PossibleDuplicateOf = await _repository.FindDuplicateAsync(input.Date,
                totals.CashCollectedCents, totals.TransferCollectedCents, totals.TheoreticalSalesCents,
                cancellationToken);
        }

        if (totals.AdjustedDifferenceCents != 0)
        {
            var shortage = totals.AdjustedDifferenceCents < 0;
            closing.Discrepancy = new CashDiscrepancy
            {
                Id = Guid.NewGuid(),
                ClosingId = closingId,
                Date = input.Date,
                Kind = shortage ? CashDiscrepancyKind.Shortage : CashDiscrepancyKind.Surplus,
                AmountCents = Math.Abs(totals.AdjustedDifferenceCents),
                EmployeeId = employee?.Id,
                Note = notes,
                Status = shortage ? CashDiscrepancyStatus.Pending : CashDiscrepancyStatus.Recorded,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        return Result.Ok((closing, stockMovements, cashMovements));

        CashMovement NewCash(Guid cashBoxId, Guid envelopeId, CashMovementKind kind, long amountCents,
            string description) => new()
        {
            Id = Guid.NewGuid(),
            CashBoxId = cashBoxId,
            EnvelopeId = envelopeId,
            Date = input.Date,
            Kind = kind,
            AmountCents = amountCents,
            Description = description,
            ClosingId = closingId,
            CreatedAt = now
        };
    }
}
