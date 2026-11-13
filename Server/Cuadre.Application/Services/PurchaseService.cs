using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using Cuadre.Application.Validators;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IPurchaseService
{
    /// <summary>Validates, computes landed costs, confirms (stock + cash + costs) and returns the price suggestions.</summary>
    Task<Result<PurchaseResult>> CreateAsync(Purchase purchase, CancellationToken cancellationToken = default);

    Task<Result<Purchase>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PurchaseSummary>> GetAllAsync(GetAllPurchasesOptions options, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(GetAllPurchasesOptions options, CancellationToken cancellationToken = default);

    /// <summary>Always allowed (decision 2026-11-12): reverses stock and cash, replays the costs.</summary>
    Task<Result<Purchase>> VoidAsync(VoidPurchase request, CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<ProductPurchase>>> GetProductPurchasesAsync(Guid productId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<SupplierProduct>>> GetSupplierProductsAsync(Guid supplierId,
        CancellationToken cancellationToken = default);
}

public class PurchaseService : IPurchaseService
{
    private readonly IPurchaseRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICashBoxRepository _cashBoxRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IBusinessSettingsRepository _settingsRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly IEnvelopeRepository _envelopeRepository;
    private readonly IValidator<Purchase> _validator;
    private readonly IValidator<VoidPurchase> _voidValidator;
    private readonly IValidator<GetAllPurchasesOptions> _optionsValidator;
    private readonly TimeProvider _clock;

    public PurchaseService(IPurchaseRepository repository, IProductRepository productRepository,
        ISupplierRepository supplierRepository, ICashBoxRepository cashBoxRepository,
        ILocationRepository locationRepository, IBusinessSettingsRepository settingsRepository,
        IExchangeRateRepository exchangeRateRepository, IEnvelopeRepository envelopeRepository,
        IValidator<Purchase> validator,
        IValidator<VoidPurchase> voidValidator, IValidator<GetAllPurchasesOptions> optionsValidator,
        TimeProvider clock)
    {
        _repository = repository;
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _cashBoxRepository = cashBoxRepository;
        _locationRepository = locationRepository;
        _settingsRepository = settingsRepository;
        _exchangeRateRepository = exchangeRateRepository;
        _envelopeRepository = envelopeRepository;
        _validator = validator;
        _voidValidator = voidValidator;
        _optionsValidator = optionsValidator;
        _clock = clock;
    }

    public async Task<Result<PurchaseResult>> CreateAsync(Purchase purchase,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(purchase, cancellationToken);

        var today = _clock.Today();
        if (purchase.Date > today)
        {
            return Result.Fail(new BusinessRuleError("La fecha de la compra no puede ser futura."));
        }

        var referencesCheck = await CheckReferencesAsync(purchase, cancellationToken);
        if (referencesCheck.IsFailed)
        {
            return referencesCheck;
        }

        var productsCheck = await ResolveLinesAsync(purchase, cancellationToken);
        if (productsCheck.IsFailed)
        {
            return productsCheck;
        }

        PurchaseCalculator.Calculate(purchase);

        var now = _clock.UtcNow();
        purchase.Status = PurchaseStatus.Confirmed;
        purchase.CreatedAt = now;
        purchase.UpdatedAt = now;
        purchase.Number = await _repository.GetNextNumberAsync(cancellationToken);

        await _repository.CreateAsync(purchase, EntryStockMovements(purchase, now), PaymentCashMovements(purchase, await ReplenishmentIdAsync(cancellationToken), now),
            now, cancellationToken);

        var saved = await _repository.GetByIdAsync(purchase.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The purchase was not found right after saving it.");

        var suggestions = await BuildSuggestionsAsync(saved, today, cancellationToken);
        return Result.Ok(new PurchaseResult(saved, suggestions));
    }

    public async Task<Result<Purchase>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var purchase = await _repository.GetByIdAsync(id, cancellationToken);
        return purchase is null
            ? Result.Fail(NotFoundError.For<Purchase>(id))
            : Result.Ok(purchase);
    }

    public async Task<IEnumerable<PurchaseSummary>> GetAllAsync(GetAllPurchasesOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
    }

    public Task<int> GetCountAsync(GetAllPurchasesOptions options, CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }

    public async Task<Result<Purchase>> VoidAsync(VoidPurchase request, CancellationToken cancellationToken = default)
    {
        var purchase = await _repository.GetByIdAsync(request.PurchaseId, cancellationToken);
        if (purchase is null)
        {
            return Result.Fail(NotFoundError.For<Purchase>(request.PurchaseId));
        }

        await _voidValidator.ValidateOrThrowAsync(request, cancellationToken);

        if (purchase.Status == PurchaseStatus.Voided)
        {
            return Result.Fail(new BusinessRuleError($"La compra #{purchase.Number} ya está anulada."));
        }

        var now = _clock.UtcNow();
        var today = _clock.Today();
        purchase.Status = PurchaseStatus.Voided;
        purchase.VoidReason = request.Reason.Trim();
        purchase.VoidedAt = now;
        purchase.UpdatedAt = now;

        await _repository.VoidAsync(purchase, ReversalStockMovements(purchase, today, now),
            ReversalCashMovements(purchase, await ReplenishmentIdAsync(cancellationToken), today, now), now, cancellationToken);

        return Result.Ok(await _repository.GetByIdAsync(purchase.Id, cancellationToken) ?? purchase);
    }

    public async Task<Result<IEnumerable<ProductPurchase>>> GetProductPurchasesAsync(Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (await _productRepository.GetByIdAsync(productId, cancellationToken) is null)
        {
            return Result.Fail(NotFoundError.For<Product>(productId));
        }

        return Result.Ok(await _repository.GetProductPurchasesAsync(productId, cancellationToken));
    }

    public async Task<Result<IEnumerable<SupplierProduct>>> GetSupplierProductsAsync(Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        if (await _supplierRepository.GetByIdAsync(supplierId, cancellationToken) is null)
        {
            return Result.Fail(NotFoundError.For<Supplier>(supplierId));
        }

        return Result.Ok(await _repository.GetSupplierProductsAsync(supplierId, cancellationToken));
    }

    /// <summary>Supplier, location and every cash box: they exist, are active, and the currencies match.</summary>
    private async Task<Result> CheckReferencesAsync(Purchase purchase, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(purchase.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Fail(new BusinessRuleError("El proveedor elegido no existe."));
        }

        if (!supplier.IsActive)
        {
            return Result.Fail(new BusinessRuleError($"El proveedor '{supplier.Name}' está desactivado."));
        }

        var location = purchase.LocationId == Guid.Empty
            ? await _locationRepository.GetDefaultStorageAsync(cancellationToken)
            : await _locationRepository.GetByIdAsync(purchase.LocationId, cancellationToken);
        if (location is null || !location.IsActive)
        {
            return Result.Fail(new BusinessRuleError("La ubicación donde entra la mercancía no existe o está desactivada."));
        }

        purchase.LocationId = location.Id;

        var boxCheck = await CheckCashBoxAsync(purchase.CashBoxId, purchase.Currency, "la compra", cancellationToken);
        if (boxCheck.IsFailed)
        {
            return boxCheck;
        }

        foreach (var charge in purchase.Charges)
        {
            var chargeCheck = await CheckCashBoxAsync(charge.CashBoxId, charge.Currency,
                $"el gasto '{charge.Description.Trim()}'", cancellationToken);
            if (chargeCheck.IsFailed)
            {
                return chargeCheck;
            }
        }

        return Result.Ok();
    }

    private async Task<Result> CheckCashBoxAsync(Guid cashBoxId, Currency currency, string paidFor,
        CancellationToken cancellationToken)
    {
        var box = await _cashBoxRepository.GetByIdAsync(cashBoxId, cancellationToken);
        if (box is null)
        {
            return Result.Fail(new BusinessRuleError($"La caja elegida para {paidFor} no existe."));
        }

        if (!box.IsActive)
        {
            return Result.Fail(new BusinessRuleError($"La caja '{box.Name}' está desactivada."));
        }

        // A CUP box cannot pay USD: the money that leaves must be the money that was in it.
        if (box.Currency != currency)
        {
            return Result.Fail(new BusinessRuleError(
                $"{Capitalize(paidFor)} es en {currency}, pero la caja '{box.Name}' es de {box.Currency}."));
        }

        return Result.Ok();
    }

    /// <summary>
    /// Loads each product, snapshots its units, resolves the factor (override ?? product) and checks that the
    /// quantities make sense for the units: no half box, and the stock quantity has to be exact.
    /// </summary>
    private async Task<Result> ResolveLinesAsync(Purchase purchase, CancellationToken cancellationToken)
    {
        var duplicated = purchase.Lines.GroupBy(line => line.ProductId).FirstOrDefault(group => group.Count() > 1);
        if (duplicated is not null)
        {
            return Result.Fail(new BusinessRuleError(
                "Un producto aparece dos veces en la compra. Juntá las cantidades en una sola línea."));
        }

        var lineNo = 1;
        foreach (var line in purchase.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null)
            {
                return Result.Fail(new BusinessRuleError($"El producto de la línea {lineNo} no existe."));
            }

            if (!product.IsActive)
            {
                return Result.Fail(new BusinessRuleError($"El producto '{product.Name}' está desactivado."));
            }

            line.PurchaseId = purchase.Id;
            line.LineNo = lineNo++;
            line.PurchaseUnit = product.PurchaseUnit;
            line.StockUnit = product.StockUnit;
            line.ConversionFactorMilli ??= product.ConversionFactorMilli;

            if (product.PurchaseUnit == product.StockUnit && line.ConversionFactorMilli != Units.MilliPerUnit)
            {
                return Result.Fail(new BusinessRuleError(
                    $"'{product.Name}' se compra y se vende en la misma unidad: el factor tiene que ser 1."));
            }

            if (!product.PurchaseUnit.AllowsDecimals() && line.QuantityMilli % Units.MilliPerUnit != 0)
            {
                return Result.Fail(new BusinessRuleError(
                    $"'{product.Name}' se compra por unidades enteras: la cantidad no puede llevar decimales."));
            }

            var stockQuantity = PurchaseCalculator.StockQuantityMilli(line.QuantityMilli, line.ConversionFactorMilli.Value);
            if (stockQuantity is null
                || (!product.StockUnit.AllowsDecimals() && stockQuantity.Value % Units.MilliPerUnit != 0))
            {
                return Result.Fail(new BusinessRuleError(
                    $"La cantidad de '{product.Name}' no da un número exacto de unidades de stock. Revisá la cantidad o el factor."));
            }
        }

        foreach (var charge in purchase.Charges)
        {
            charge.PurchaseId = purchase.Id;
        }

        return Result.Ok();
    }

    /// <summary>Products whose suggested price (with the NEW average cost) differs from their current price.</summary>
    private async Task<IReadOnlyList<PriceSuggestion>> BuildSuggestionsAsync(Purchase purchase, DateOnly today,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken)
                       ?? throw new InvalidOperationException("Business settings are missing.");
        var todayRate = (await _exchangeRateRepository.GetLatestOnOrBeforeAsync(today, cancellationToken))?.CupPerUsdCents;

        var suggestions = new List<PriceSuggestion>();
        foreach (var line in purchase.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || ProductPricingRules.Build(product, settings, todayRate) is not { } pricing)
            {
                continue;
            }

            if (pricing.SuggestedPriceCents != product.SalePriceCents)
            {
                suggestions.Add(new PriceSuggestion(product.Id, product.Name, product.SalePriceCents,
                    pricing.SuggestedPriceCents));
            }
        }

        return suggestions;
    }

    private static List<StockMovement> EntryStockMovements(Purchase purchase, DateTime now) =>
        purchase.Lines.Select(line => new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = line.ProductId,
            LocationId = purchase.LocationId,
            Date = purchase.Date,
            Kind = StockMovementKind.Purchase,
            QuantityMilli = line.StockQuantityMilli,
            UnitCostUsdMicros = line.LandedUnitCostUsdMicros,
            UnitCostCupMicros = line.LandedUnitCostCupMicros,
            PurchaseId = purchase.Id,
            PurchaseLineId = line.Id,
            CreatedAt = now
        }).ToList();

    /// <summary>Purchases and their charges always pay from Reposición (decision 2026-11-12): it receives A2.</summary>
    private async Task<Guid> ReplenishmentIdAsync(CancellationToken cancellationToken) =>
        (await _envelopeRepository.GetByRoleAsync(EnvelopeRole.Replenishment, cancellationToken)).Id;

    private static List<CashMovement> PaymentCashMovements(Purchase purchase, Guid envelopeId, DateTime now)
    {
        var movements = new List<CashMovement>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CashBoxId = purchase.CashBoxId,
                EnvelopeId = envelopeId,
                Date = purchase.Date,
                Kind = CashMovementKind.Purchase,
                AmountCents = -purchase.GoodsTotalCents,
                Description = $"Compra #{purchase.Number}",
                PurchaseId = purchase.Id,
                CreatedAt = now
            }
        };

        movements.AddRange(purchase.Charges.Select(charge => new CashMovement
        {
            Id = Guid.NewGuid(),
            CashBoxId = charge.CashBoxId,
            EnvelopeId = envelopeId,
            Date = purchase.Date,
            Kind = CashMovementKind.PurchaseCharge,
            AmountCents = -charge.AmountCents,
            Description = charge.Description.Trim(),
            PurchaseId = purchase.Id,
            CreatedAt = now
        }));

        return movements;
    }

    /// <summary>The goods leave the location where they entered. Dated TODAY: the past is not rewritten.</summary>
    private static List<StockMovement> ReversalStockMovements(Purchase purchase, DateOnly today, DateTime now) =>
        purchase.Lines.Select(line => new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = line.ProductId,
            LocationId = purchase.LocationId,
            Date = today,
            Kind = StockMovementKind.PurchaseVoid,
            QuantityMilli = -line.StockQuantityMilli,
            PurchaseId = purchase.Id,
            PurchaseLineId = line.Id,
            CreatedAt = now
        }).ToList();

    private static List<CashMovement> ReversalCashMovements(Purchase purchase, Guid envelopeId, DateOnly today,
        DateTime now)
    {
        var movements = new List<CashMovement>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CashBoxId = purchase.CashBoxId,
                EnvelopeId = envelopeId,
                Date = today,
                Kind = CashMovementKind.PurchaseVoid,
                AmountCents = purchase.GoodsTotalCents,
                Description = $"Anulación compra #{purchase.Number}",
                PurchaseId = purchase.Id,
                CreatedAt = now
            }
        };

        movements.AddRange(purchase.Charges.Select(charge => new CashMovement
        {
            Id = Guid.NewGuid(),
            CashBoxId = charge.CashBoxId,
            EnvelopeId = envelopeId,
            Date = today,
            Kind = CashMovementKind.PurchaseVoid,
            AmountCents = charge.AmountCents,
            Description = $"Anulación compra #{purchase.Number}: {charge.Description}",
            PurchaseId = purchase.Id,
            CreatedAt = now
        }));

        return movements;
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];
}
