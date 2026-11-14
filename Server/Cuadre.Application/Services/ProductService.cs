using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

public interface IProductService
{
    /// <summary>A blank code is generated from the category prefix (ARR-001...).</summary>
    Task<Result<Product>> CreateAsync(Product product, CancellationToken cancellationToken = default);

    Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetAllAsync(GetAllProductsOptions options, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(GetAllProductsOptions options, CancellationToken cancellationToken = default);
    Task<Result<Product>> UpdateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>What the code of a new product in that category would be (to prefill the form).</summary>
    Task<Result<string>> GetNextCodeAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<ProductPriceChange>>> GetPriceHistoryAsync(Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>Accepts a suggested price after a purchase (reason Purchase) or a quick manual change.</summary>
    Task<Result<Product>> SetSalePriceAsync(SetSalePrice request, CancellationToken cancellationToken = default);

    /// <summary>D-61: prices to look at, because a new batch went on sale or the next one will not fit.</summary>
    Task<IReadOnlyList<PriceNotice>> GetPriceNoticesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// D-61: take the notice's suggested price (in force from today, like any price change) or keep the price.
    /// Either way the notice goes away until the next batch. A notice that no longer applies is a no-op.
    /// </summary>
    Task<Result<Product>> ResolvePriceNoticeAsync(ResolvePriceNotice request,
        CancellationToken cancellationToken = default);
}

public class ProductService : IProductService
{
    /// <summary>Every product in one page: the notices look at the whole catalog (a few hundred rows).</summary>
    private const int AllProducts = 100_000;

    private readonly IProductRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IValidator<Product> _validator;
    private readonly IValidator<GetAllProductsOptions> _optionsValidator;
    private readonly IValidator<SetSalePrice> _salePriceValidator;
    private readonly IBusinessSettingsRepository _settingsRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly TimeProvider _clock;

    public ProductService(IProductRepository repository, ICategoryRepository categoryRepository,
        ISupplierRepository supplierRepository, IValidator<Product> validator,
        IValidator<GetAllProductsOptions> optionsValidator, IValidator<SetSalePrice> salePriceValidator,
        IBusinessSettingsRepository settingsRepository, IExchangeRateRepository exchangeRateRepository,
        TimeProvider clock)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _supplierRepository = supplierRepository;
        _validator = validator;
        _optionsValidator = optionsValidator;
        _salePriceValidator = salePriceValidator;
        _settingsRepository = settingsRepository;
        _exchangeRateRepository = exchangeRateRepository;
        _clock = clock;
    }

    public async Task<Result<Product>> CreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(product.Code) && product.CategoryId != Guid.Empty)
        {
            var category = await _categoryRepository.GetByIdAsync(product.CategoryId, cancellationToken);
            if (category is { IsTopLevel: true })
            {
                product.Code = await NextCodeAsync(category.CodePrefix!, cancellationToken);
            }
        }

        await _validator.ValidateOrThrowAsync(product, cancellationToken);

        var ruleCheck = await CheckDataRulesAsync(product, previousSupplierId: null, cancellationToken);
        if (ruleCheck.IsFailed)
        {
            return ruleCheck;
        }

        var now = _clock.UtcNow();
        product.CreatedAt = now;
        product.UpdatedAt = now;

        var priceChange = product.SalePriceCents is { } price
            ? NewPriceChange(product.Id, price, previousPriceCents: null, now)
            : null;

        await _repository.CreateAsync(product, priceChange, cancellationToken);
        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Fail(NotFoundError.For<Product>(id));
        }

        await AttachPricingAsync([product], cancellationToken);
        return Result.Ok(product);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(GetAllProductsOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        var products = (await _repository.GetAllAsync(options, cancellationToken)).ToList();
        await AttachPricingAsync(products, cancellationToken);
        return products;
    }

    public Task<int> GetCountAsync(GetAllProductsOptions options, CancellationToken cancellationToken = default)
    {
        return _repository.GetCountAsync(options, cancellationToken);
    }

    public async Task<Result<Product>> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(product.Id, cancellationToken);
        if (existing is null)
        {
            return Result.Fail(NotFoundError.For<Product>(product.Id));
        }

        await _validator.ValidateOrThrowAsync(product, cancellationToken);

        // Once on the price list, "no price" would leave a hole on the sign and break the history.
        if (existing.SalePriceCents is not null && product.SalePriceCents is null)
        {
            return Result.Fail(new BusinessRuleError(
                "Un producto que ya tiene precio no puede quedar sin precio. Cambialo por el nuevo valor."));
        }

        // The ledger stores quantities in the stock unit: changing it would silently turn 100 lb into 100 kg.
        if (product.StockUnit != existing.StockUnit
            && await _repository.HasStockMovementsAsync(product.Id, cancellationToken))
        {
            return Result.Fail(new BusinessRuleError(
                "Este producto ya tiene movimientos de stock: no se puede cambiar la unidad en que se vende."));
        }

        var ruleCheck = await CheckDataRulesAsync(product, existing.UsualSupplierId, cancellationToken);
        if (ruleCheck.IsFailed)
        {
            return ruleCheck;
        }

        var now = _clock.UtcNow();
        product.CreatedAt = existing.CreatedAt;
        product.UpdatedAt = now;
        product.ListedPriceCents = existing.ListedPriceCents;

        var priceChange = product.SalePriceCents is { } price && price != existing.SalePriceCents
            ? NewPriceChange(product.Id, price, existing.SalePriceCents, now)
            : null;

        await _repository.UpdateAsync(product, priceChange, cancellationToken);
        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<Result<string>> GetNextCodeAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category is null)
        {
            return Result.Fail(NotFoundError.For<Category>(categoryId));
        }

        if (!category.IsTopLevel)
        {
            return Result.Fail(new BusinessRuleError("El código se arma con la categoría, no con la subcategoría."));
        }

        return Result.Ok(await NextCodeAsync(category.CodePrefix!, cancellationToken));
    }

    public async Task<Result<IEnumerable<ProductPriceChange>>> GetPriceHistoryAsync(Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetByIdAsync(productId, cancellationToken) is null)
        {
            return Result.Fail(NotFoundError.For<Product>(productId));
        }

        return Result.Ok(await _repository.GetPriceHistoryAsync(productId, cancellationToken));
    }

    public async Task<Result<Product>> SetSalePriceAsync(SetSalePrice request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (existing is null)
        {
            return Result.Fail(NotFoundError.For<Product>(request.ProductId));
        }

        await _salePriceValidator.ValidateOrThrowAsync(request, cancellationToken);

        // Same price: nothing to record (accepting a suggestion twice must not duplicate the history).
        if (existing.SalePriceCents != request.SalePriceCents)
        {
            var now = _clock.UtcNow();
            var priceChange = NewPriceChange(existing.Id, request.SalePriceCents, existing.SalePriceCents, now,
                request.PurchaseId is null ? PriceChangeReason.Manual : PriceChangeReason.Purchase);
            await _repository.UpdateSalePriceAsync(existing.Id, request.SalePriceCents, now, priceChange,
                cancellationToken);
        }

        return await GetByIdAsync(existing.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceNotice>> GetPriceNoticesAsync(CancellationToken cancellationToken = default)
    {
        var products = (await _repository.GetAllAsync(
            new GetAllProductsOptions { Page = 1, PageSize = AllProducts }, cancellationToken)).ToList();
        await AttachPricingAsync(products, cancellationToken);

        return products
            .Where(product => product.Pricing is not null)
            .SelectMany(product => PriceNotices.For(product, product.Pricing!))
            .OrderBy(notice => notice.Kind)
            .ThenBy(notice => notice.ProductName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Result<Product>> ResolvePriceNoticeAsync(ResolvePriceNotice request,
        CancellationToken cancellationToken = default)
    {
        var found = await GetByIdAsync(request.ProductId, cancellationToken);
        if (found.IsFailed)
        {
            return found;
        }

        var product = found.Value;
        var notice = product.Pricing is { } pricing
            ? PriceNotices.For(product, pricing).FirstOrDefault(n => n.Kind == request.Kind)
            : null;
        if (notice is null)
        {
            return found;
        }

        if (request.AcceptSuggested && notice.SuggestedPriceCents != product.SalePriceCents)
        {
            var now = _clock.UtcNow();
            await _repository.UpdateSalePriceAsync(product.Id, notice.SuggestedPriceCents, now,
                NewPriceChange(product.Id, notice.SuggestedPriceCents, product.SalePriceCents, now), cancellationToken);
        }

        if (request.Kind == PriceNoticeKind.BatchChanged)
        {
            await _repository.MarkPriceReviewedAsync(product.Id, product.CurrentPurchaseLineId, cancellationToken);
        }
        else
        {
            await _repository.MarkCostlierSeenAsync(product.Id, product.LastPurchaseLineId, cancellationToken);
        }

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    /// <summary>One settings read and one rate read for the whole page, then pure math per product.</summary>
    private async Task AttachPricingAsync(IReadOnlyCollection<Product> products, CancellationToken cancellationToken)
    {
        if (products.All(product => product.AverageCost is null))
        {
            return;
        }

        var settings = await _settingsRepository.GetAsync(cancellationToken)
                       ?? throw new InvalidOperationException("Business settings are missing.");
        var todayRate = (await _exchangeRateRepository.GetLatestOnOrBeforeAsync(_clock.Today(), cancellationToken))
            ?.CupPerUsdCents;

        foreach (var product in products)
        {
            product.Pricing = ProductPricingRules.Build(product, settings, todayRate);
        }
    }

    /// <summary>
    /// 422 for references from the body that are wrong (category, subcategory, supplier);
    /// 409 for a code or barcode already taken by another product.
    /// </summary>
    private async Task<Result<Product>> CheckDataRulesAsync(Product product, Guid? previousSupplierId,
        CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(product.CategoryId, cancellationToken);
        if (category is null || !category.IsTopLevel)
        {
            return Result.Fail(new BusinessRuleError("La categoría elegida no existe."));
        }

        if (product.SubcategoryId is { } subcategoryId)
        {
            var subcategory = await _categoryRepository.GetByIdAsync(subcategoryId, cancellationToken);
            if (subcategory is null || subcategory.ParentId != category.Id)
            {
                return Result.Fail(new BusinessRuleError(
                    $"La subcategoría elegida no pertenece a '{category.Name}'."));
            }
        }

        // An inactive supplier may stay where it already was, but cannot be newly chosen.
        if (product.UsualSupplierId is { } supplierId)
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId, cancellationToken);
            if (supplier is null)
            {
                return Result.Fail(new BusinessRuleError("El proveedor elegido no existe."));
            }

            if (!supplier.IsActive && supplierId != previousSupplierId)
            {
                return Result.Fail(new BusinessRuleError($"El proveedor '{supplier.Name}' está desactivado."));
            }
        }

        var sameCode = await _repository.GetByCodeAsync(product.Code, cancellationToken);
        if (sameCode is not null && sameCode.Id != product.Id)
        {
            return Result.Fail(new ConflictError($"El código '{product.Code}' ya lo tiene '{sameCode.Name}'."));
        }

        if (product.Barcode is not null)
        {
            var sameBarcode = await _repository.GetByBarcodeAsync(product.Barcode, cancellationToken);
            if (sameBarcode is not null && sameBarcode.Id != product.Id)
            {
                return Result.Fail(new ConflictError(
                    $"Ese código de barras ya lo tiene '{sameBarcode.Name}'."));
            }
        }

        return Result.Ok(product);
    }

    private async Task<string> NextCodeAsync(string prefix, CancellationToken cancellationToken)
    {
        var codes = await _repository.GetCodesWithPrefixAsync(prefix, cancellationToken);
        return ProductCode.Next(prefix, codes);
    }

    private ProductPriceChange NewPriceChange(Guid productId, long priceCents, long? previousPriceCents,
        DateTime now, PriceChangeReason reason = PriceChangeReason.Manual) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        PriceCents = priceCents,
        PreviousPriceCents = previousPriceCents,
        EffectiveDate = _clock.Today(),
        Reason = reason,
        CreatedAt = now
    };
}
