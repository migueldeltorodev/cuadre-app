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
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IValidator<Product> _validator;
    private readonly IValidator<GetAllProductsOptions> _optionsValidator;
    private readonly TimeProvider _clock;

    public ProductService(IProductRepository repository, ICategoryRepository categoryRepository,
        ISupplierRepository supplierRepository, IValidator<Product> validator,
        IValidator<GetAllProductsOptions> optionsValidator, TimeProvider clock)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _supplierRepository = supplierRepository;
        _validator = validator;
        _optionsValidator = optionsValidator;
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
        return Result.Ok(product);
    }

    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        return product is null
            ? Result.Fail(NotFoundError.For<Product>(id))
            : Result.Ok(product);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(GetAllProductsOptions options,
        CancellationToken cancellationToken = default)
    {
        await _optionsValidator.ValidateOrThrowAsync(options, cancellationToken);
        return await _repository.GetAllAsync(options, cancellationToken);
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
        return Result.Ok(product);
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
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        PriceCents = priceCents,
        PreviousPriceCents = previousPriceCents,
        EffectiveDate = _clock.Today(),
        Reason = PriceChangeReason.Manual,
        CreatedAt = now
    };
}
