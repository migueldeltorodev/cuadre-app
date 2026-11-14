using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IStockOperationService
{
    /// <summary>Transfer, exit or count: validates, converts units, confirms (movements + costs), returns warnings.</summary>
    Task<Result<StockOperationResult>> CreateAsync(StockOperation operation,
        CancellationToken cancellationToken = default);

    Task<Result<StockOperation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<StockOperationSummary>> GetAllAsync(GetAllStockOperationsOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllStockOperationsOptions options, CancellationToken cancellationToken = default);

    /// <summary>Always allowed (decision 2026-11-12): writes the reversal movements and replays the costs.</summary>
    Task<Result<StockOperation>> VoidAsync(VoidStockOperation request, CancellationToken cancellationToken = default);
}

public class StockOperationService : IStockOperationService
{
    private readonly IStockOperationRepository _repository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly ICostingRepository _costingRepository;
    private readonly IBusinessSettingsRepository _settingsRepository;
    private readonly IValidator<StockOperation> _validator;
    private readonly IValidator<VoidStockOperation> _voidValidator;
    private readonly IValidator<GetAllStockOperationsOptions> _optionsValidator;
    private readonly IClosedDayGuard _closedDayGuard;
    private readonly TimeProvider _clock;

    public StockOperationService(IStockOperationRepository repository, IInventoryRepository inventoryRepository,
        IProductRepository productRepository, ILocationRepository locationRepository,
        IEnvelopeRepository envelopeRepository, ICashBoxRepository cashBoxRepository,
        ICostingRepository costingRepository, IBusinessSettingsRepository settingsRepository,
        IValidator<StockOperation> validator, IValidator<VoidStockOperation> voidValidator,
        IValidator<GetAllStockOperationsOptions> optionsValidator, IClosedDayGuard closedDayGuard, TimeProvider clock)
    {
        _repository = repository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _locationRepository = locationRepository;
        _envelopeRepository = envelopeRepository;
        _cashBoxRepository = cashBoxRepository;
        _costingRepository = costingRepository;
        _settingsRepository = settingsRepository;
        _validator = validator;
        _voidValidator = voidValidator;
        _optionsValidator = optionsValidator;
        _closedDayGuard = closedDayGuard;
        _clock = clock;
    }

    public async Task<Result<StockOperationResult>> CreateAsync(StockOperation operation,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(operation, cancellationToken);

        if (operation.Date > _clock.Today())
        {
            return Result.Fail(new BusinessRuleError("La fecha no puede ser futura."));
        }

        var open = await _closedDayGuard.EnsureOpenAsync(operation.Date, cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var locationsCheck = await CheckLocationsAsync(operation, cancellationToken);
        if (locationsCheck.IsFailed)
        {
            return locationsCheck;
        }

        var linesCheck = await ResolveLinesAsync(operation, cancellationToken);
        if (linesCheck.IsFailed)
        {
            return linesCheck;
        }

        // Before saving: the balance the operation starts from (all dates: "how much is there now").
        IReadOnlyDictionary<Guid, long> before = operation.Kind == StockOperationKind.Count
            ? new Dictionary<Guid, long>()
            : await _inventoryRepository.GetBalancesAsync(operation.LocationId,
                operation.Lines.Select(line => line.ProductId), cancellationToken: cancellationToken);

        var now = _clock.UtcNow();
        operation.Status = StockOperationStatus.Confirmed;
        operation.CreatedAt = now;
        operation.UpdatedAt = now;
        operation.Number = await _repository.GetNextNumberAsync(cancellationToken);

        var cashMovements = await HouseConsumptionMovementsAsync(operation, now, cancellationToken);
        if (cashMovements.IsFailed)
        {
            return cashMovements.ToResult();
        }

        await _repository.CreateAsync(operation, Movements(operation, now), cashMovements.Value, now,
            cancellationToken);

        var saved = await _repository.GetByIdAsync(operation.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The stock operation was not found right after saving it.");

        List<NegativeStockWarning> warnings = saved.Kind == StockOperationKind.Count
            ? []
            : saved.Lines
                .Select(line => new NegativeStockWarning(line.ProductId, line.ProductName, line.StockUnit,
                    saved.LocationId, saved.LocationName,
                    before.GetValueOrDefault(line.ProductId) - line.QuantityMilli))
                .Where(warning => warning.BalanceMilli < 0)
                .ToList();

        return Result.Ok(new StockOperationResult(saved, warnings));
    }

    public async Task<Result<StockOperation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operation = await _repository.GetByIdAsync(id, cancellationToken);
        return operation is null
            ? Result.Fail(NotFoundError.For<StockOperation>(id))
            : Result.Ok(operation);
    }

    public async Task<IEnumerable<StockOperationSummary>> GetAllAsync(GetAllStockOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllStockOperationsOptions options, CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }

    public async Task<Result<StockOperation>> VoidAsync(VoidStockOperation request,
        CancellationToken cancellationToken = default)
    {
        var operation = await _repository.GetByIdAsync(request.OperationId, cancellationToken);
        if (operation is null)
        {
            return Result.Fail(NotFoundError.For<StockOperation>(request.OperationId));
        }

        await _voidValidator.ValidateOrThrowAsync(request, cancellationToken);

        if (operation.Status == StockOperationStatus.Voided)
        {
            return Result.Fail(new BusinessRuleError($"La operación #{operation.Number} ya está anulada."));
        }

        // The reversal is dated today: today must still be open.
        var open = await _closedDayGuard.EnsureOpenAsync(_clock.Today(), cancellationToken);
        if (open.IsFailed)
        {
            return open;
        }

        var now = _clock.UtcNow();
        operation.Status = StockOperationStatus.Voided;
        operation.VoidReason = request.Reason.Trim();
        operation.VoidedAt = now;
        operation.UpdatedAt = now;

        // The envelope move of a house consumption goes back too, in the same box and envelopes.
        var cashReversals = (await _repository.GetCashMovementsAsync(operation.Id, cancellationToken))
            .Select(movement => new CashMovement
            {
                Id = Guid.NewGuid(),
                CashBoxId = movement.CashBoxId,
                EnvelopeId = movement.EnvelopeId,
                Date = _clock.Today(),
                Kind = CashMovementKind.StockOperationVoid,
                AmountCents = -movement.AmountCents,
                Description = $"Anulación de la salida #{operation.Number}",
                StockOperationId = operation.Id,
                CreatedAt = now
            })
            .ToList();

        await _repository.VoidAsync(operation, ReversalMovements(operation, _clock.Today(), now), cashReversals, now,
            cancellationToken);

        return Result.Ok(await _repository.GetByIdAsync(operation.Id, cancellationToken) ?? operation);
    }

    /// <summary>
    /// Consumo de la casa (RN-11, D-13): an owner's withdrawal in kind. Its value (FIFO + colchón, like A2: D-60)
    /// leaves the Dueño envelope and goes to Reposición, because those goods have to be bought again. No cash actually moves, so both
    /// rows go to the same box: the main CUP cash box. Products never bought have no cost and move nothing.
    /// </summary>
    private async Task<Result<List<CashMovement>>> HouseConsumptionMovementsAsync(StockOperation operation,
        DateTime now, CancellationToken cancellationToken)
    {
        if (operation.Kind != StockOperationKind.Exit || operation.ExitReason != ExitReason.HouseConsumption)
        {
            return Result.Ok(new List<CashMovement>());
        }

        var valueCents = operation.Lines.Sum(line => line.ValueCupCents ?? 0);
        if (valueCents <= 0)
        {
            return Result.Ok(new List<CashMovement>());
        }

        var box = (await _cashBoxRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(b => b.IsActive && b.Currency == Currency.CUP && b.Kind == CashBoxKind.Cash);
        if (box is null)
        {
            return Result.Fail(new BusinessRuleError(
                "Hace falta una caja de efectivo en CUP activa para pasar el consumo de la casa de Dueño a Reposición."));
        }

        var owner = await _envelopeRepository.GetByRoleAsync(EnvelopeRole.OwnerPay, cancellationToken);
        var replenishment = await _envelopeRepository.GetByRoleAsync(EnvelopeRole.Replenishment, cancellationToken);
        var description = $"Consumo de la casa (salida #{operation.Number})";

        return Result.Ok(new List<CashMovement>
        {
            NewCashMovement(owner.Id, -valueCents),
            NewCashMovement(replenishment.Id, valueCents)
        });

        CashMovement NewCashMovement(Guid envelopeId, long amountCents) => new()
        {
            Id = Guid.NewGuid(),
            CashBoxId = box.Id,
            EnvelopeId = envelopeId,
            Date = operation.Date,
            Kind = CashMovementKind.HouseConsumption,
            AmountCents = amountCents,
            Description = description,
            StockOperationId = operation.Id,
            CreatedAt = now
        };
    }

    private async Task<Result> CheckLocationsAsync(StockOperation operation, CancellationToken cancellationToken)
    {
        var location = await _locationRepository.GetByIdAsync(operation.LocationId, cancellationToken);
        if (location is null || !location.IsActive)
        {
            return Result.Fail(new BusinessRuleError(operation.Kind == StockOperationKind.Transfer
                ? "La ubicación de origen no existe o está desactivada."
                : "La ubicación elegida no existe o está desactivada."));
        }

        if (operation.ToLocationId is { } toLocationId)
        {
            var destination = await _locationRepository.GetByIdAsync(toLocationId, cancellationToken);
            if (destination is null || !destination.IsActive)
            {
                return Result.Fail(new BusinessRuleError("La ubicación de destino no existe o está desactivada."));
            }
        }

        return Result.Ok();
    }

    /// <summary>
    /// Loads each product, converts what was typed into stock units (exact, and whole for countable units),
    /// fills the count's expected/counted and snapshots the cost where a value is needed (D-60). What leaves takes
    /// the oldest batches first, at what they cost: a loss is worth what it cost. House consumption adds the
    /// colchón, like A2, because it funds Reposición. A count surplus enters at the newest batch's cost.
    /// Inactive products are allowed: their leftover stock still has to be moved, written off or counted.
    /// </summary>
    private async Task<Result> ResolveLinesAsync(StockOperation operation, CancellationToken cancellationToken)
    {
        var duplicated = operation.Lines.GroupBy(line => line.ProductId).FirstOrDefault(group => group.Count() > 1);
        if (duplicated is not null)
        {
            return Result.Fail(new BusinessRuleError(
                "Un producto aparece dos veces. Juntá las cantidades en una sola línea."));
        }

        IReadOnlyDictionary<Guid, long> expected = operation.Kind == StockOperationKind.Count
            ? await _inventoryRepository.GetBalancesAsync(operation.LocationId,
                operation.Lines.Select(line => line.ProductId), operation.Date, cancellationToken)
            : new Dictionary<Guid, long>();

        var settings = await _settingsRepository.GetAsync(cancellationToken)
                       ?? throw new InvalidOperationException("The settings row is missing: the seeder did not run.");
        var books = operation.Kind == StockOperationKind.Transfer
            ? new Dictionary<Guid, FifoBook>()
            : await _costingRepository.GetBooksAsync(operation.Lines.Select(line => line.ProductId).ToList(),
                operation.Date, cancellationToken);

        var lineNo = 1;
        foreach (var line in operation.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null)
            {
                return Result.Fail(new BusinessRuleError($"El producto de la línea {lineNo} no existe."));
            }

            line.OperationId = operation.Id;
            line.LineNo = lineNo++;
            line.ConversionFactorMilli = product.ConversionFactorMilli;

            if (line.EnteredInPurchaseUnits && !product.PurchaseUnit.AllowsDecimals()
                                            && line.EnteredQuantityMilli % Units.MilliPerUnit != 0)
            {
                return Result.Fail(new BusinessRuleError(
                    $"'{product.Name}' se cuenta por unidades de compra enteras. Para una parte, escribila en la unidad de stock."));
            }

            var stockMilli = InventoryMath.ToStockMilli(line.EnteredQuantityMilli, line.EnteredInPurchaseUnits,
                product.ConversionFactorMilli);
            if (stockMilli is null
                || (!product.StockUnit.AllowsDecimals() && stockMilli.Value % Units.MilliPerUnit != 0))
            {
                return Result.Fail(new BusinessRuleError(
                    $"La cantidad de '{product.Name}' no da un número exacto de unidades de stock."));
            }

            if (operation.Kind == StockOperationKind.Count)
            {
                line.CountedMilli = stockMilli.Value;
                line.ExpectedMilli = expected.GetValueOrDefault(line.ProductId);
                line.QuantityMilli = line.CountedMilli.Value - line.ExpectedMilli.Value;
            }
            else
            {
                line.QuantityMilli = stockMilli.Value;
            }

            // The value of what left (exit) or of the difference found (count), at the cost of that moment.
            if (operation.Kind != StockOperationKind.Transfer && line.QuantityMilli != 0)
            {
                var book = books[line.ProductId];
                var cost = operation.Kind == StockOperationKind.Count && line.QuantityMilli > 0
                    ? book.NewestCost
                    : book.Take(Math.Abs(line.QuantityMilli), operation.Date).UnitCost(book.NewestCost);
                if (cost is not null && operation.ExitReason == ExitReason.HouseConsumption)
                {
                    cost = FifoCosting.WithBuffer(cost, product.ExchangeBufferCents ?? settings.ExchangeBufferCents);
                }

                line.UnitCostUsdMicros = cost?.UsdMicros;
                line.UnitCostCupMicros = cost?.CupMicros;
            }
        }

        return Result.Ok();
    }

    private static List<StockMovement> Movements(StockOperation operation, DateTime now)
    {
        var movements = new List<StockMovement>();
        foreach (var line in operation.Lines)
        {
            switch (operation.Kind)
            {
                case StockOperationKind.Transfer:
                    // Both rows carry no cost: moving goods between rooms does not change what they cost.
                    movements.Add(NewMovement(operation, line, operation.LocationId, StockMovementKind.TransferOut,
                        -line.QuantityMilli, operation.Date, now));
                    movements.Add(NewMovement(operation, line, operation.ToLocationId!.Value,
                        StockMovementKind.TransferIn, line.QuantityMilli, operation.Date, now));
                    break;

                case StockOperationKind.Exit:
                    // The cost rides along to value the loss; the replay ignores costs on outflows.
                    movements.Add(NewMovement(operation, line, operation.LocationId, StockMovementKind.Exit,
                        -line.QuantityMilli, operation.Date, now, withCost: true));
                    break;

                case StockOperationKind.Count when line.QuantityMilli != 0:
                    // Shortage: out at cost (a loss). Surplus: in WITHOUT cost, so it enters at the average.
                    movements.Add(NewMovement(operation, line, operation.LocationId, StockMovementKind.CountAdjustment,
                        line.QuantityMilli, operation.Date, now, withCost: line.QuantityMilli < 0));
                    break;
            }
        }

        return movements;
    }

    /// <summary>The exact opposite of <see cref="Movements"/>, dated TODAY: the past is not rewritten.</summary>
    private static List<StockMovement> ReversalMovements(StockOperation operation, DateOnly today, DateTime now)
    {
        var movements = new List<StockMovement>();
        foreach (var line in operation.Lines)
        {
            switch (operation.Kind)
            {
                case StockOperationKind.Transfer:
                    movements.Add(NewMovement(operation, line, operation.LocationId, StockMovementKind.OperationVoid,
                        line.QuantityMilli, today, now));
                    movements.Add(NewMovement(operation, line, operation.ToLocationId!.Value,
                        StockMovementKind.OperationVoid, -line.QuantityMilli, today, now));
                    break;

                case StockOperationKind.Exit:
                    movements.Add(NewMovement(operation, line, operation.LocationId, StockMovementKind.OperationVoid,
                        line.QuantityMilli, today, now));
                    break;

                case StockOperationKind.Count when line.QuantityMilli != 0:
                    movements.Add(NewMovement(operation, line, operation.LocationId, StockMovementKind.OperationVoid,
                        -line.QuantityMilli, today, now));
                    break;
            }
        }

        return movements;
    }

    private static StockMovement NewMovement(StockOperation operation, StockOperationLine line, Guid locationId,
        StockMovementKind kind, long quantityMilli, DateOnly date, DateTime now, bool withCost = false) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = line.ProductId,
        LocationId = locationId,
        Date = date,
        Kind = kind,
        QuantityMilli = quantityMilli,
        UnitCostUsdMicros = withCost ? line.UnitCostUsdMicros : null,
        UnitCostCupMicros = withCost ? line.UnitCostCupMicros : null,
        OperationId = operation.Id,
        CreatedAt = now
    };
}
