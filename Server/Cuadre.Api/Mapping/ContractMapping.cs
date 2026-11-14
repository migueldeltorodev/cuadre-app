using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>
/// The ONLY place where Contracts (wire DTOs) and Application models meet.
/// Plain extension methods: explicit, debuggable, no reflection-based mapper.
/// Timestamps are NOT set here: the services stamp them with the app clock (TimeProvider).
/// </summary>
public static partial class ContractMapping
{
    // ---------- Business settings ----------

    public static BusinessSettings MapToBusinessSettings(this UpdateBusinessSettingsRequest request)
    {
        return new BusinessSettings
        {
            Name = request.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            ClosedDays = request.ClosedDays.Select(day => (DayOfWeek)(int)day).ToArray(),
            CashDifferenceThresholdCents = request.CashDifferenceThresholdCents,
            ExchangeBufferCents = request.ExchangeBufferCents,
            DefaultMarkupBps = request.DefaultMarkupBps,
            PriceRoundingStepCents = request.PriceRoundingStepCents
        };
    }

    public static BusinessSettingsResponse MapToResponse(this BusinessSettings settings)
    {
        return new BusinessSettingsResponse
        {
            Name = settings.Name,
            Address = settings.Address,
            ClosedDays = settings.ClosedDays.Select(day => (Weekday)(int)day).ToArray(),
            CashDifferenceThresholdCents = settings.CashDifferenceThresholdCents,
            ExchangeBufferCents = settings.ExchangeBufferCents,
            DefaultMarkupBps = settings.DefaultMarkupBps,
            PriceRoundingStepCents = settings.PriceRoundingStepCents,
            UpdatedAt = settings.UpdatedAt
        };
    }

    // ---------- Cash boxes ----------

    public static CashBox MapToCashBox(this CreateCashBoxRequest request)
    {
        return new CashBox
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Currency = request.Currency.ToModel(),
            Kind = request.Kind.ToModel(),
            IsActive = true
        };
    }

    public static CashBoxChanges MapToCashBoxChanges(this UpdateCashBoxRequest request, Guid id)
    {
        return new CashBoxChanges(id, request.Name.Trim(), request.IsActive);
    }

    public static CashBoxResponse MapToResponse(this CashBox cashBox)
    {
        return new CashBoxResponse
        {
            Id = cashBox.Id,
            Name = cashBox.Name,
            Currency = cashBox.Currency.ToContract(),
            Kind = cashBox.Kind.ToContract(),
            IsActive = cashBox.IsActive,
            CreatedAt = cashBox.CreatedAt,
            UpdatedAt = cashBox.UpdatedAt
        };
    }

    public static CashBoxesResponse MapToResponse(this IEnumerable<CashBox> cashBoxes)
    {
        return new CashBoxesResponse { Items = cashBoxes.Select(MapToResponse) };
    }

    // ---------- Payment methods ----------

    public static PaymentMethod MapToPaymentMethod(this CreatePaymentMethodRequest request)
    {
        return new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CashBoxId = request.CashBoxId,
            IsActive = true
        };
    }

    public static PaymentMethod MapToPaymentMethod(this UpdatePaymentMethodRequest request, Guid id)
    {
        return new PaymentMethod
        {
            Id = id,
            Name = request.Name.Trim(),
            CashBoxId = request.CashBoxId,
            IsActive = request.IsActive
        };
    }

    public static PaymentMethodResponse MapToResponse(this PaymentMethod paymentMethod)
    {
        return new PaymentMethodResponse
        {
            Id = paymentMethod.Id,
            Name = paymentMethod.Name,
            CashBoxId = paymentMethod.CashBoxId,
            IsActive = paymentMethod.IsActive,
            CreatedAt = paymentMethod.CreatedAt,
            UpdatedAt = paymentMethod.UpdatedAt
        };
    }

    public static PaymentMethodsResponse MapToResponse(this IEnumerable<PaymentMethod> paymentMethods)
    {
        return new PaymentMethodsResponse { Items = paymentMethods.Select(MapToResponse) };
    }

    // ---------- Exchange rates ----------

    public static ExchangeRateResponse MapToResponse(this ExchangeRate rate)
    {
        return new ExchangeRateResponse
        {
            Date = rate.Date,
            CupPerUsdCents = rate.CupPerUsdCents,
            UpdatedAt = rate.UpdatedAt
        };
    }

    public static ExchangeRatesResponse MapToResponse(this IEnumerable<ExchangeRate> rates,
        int page, int pageSize, int total)
    {
        return new ExchangeRatesResponse
        {
            Items = rates.Select(MapToResponse),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static CurrentExchangeRateResponse MapToResponse(this CurrentExchangeRate current)
    {
        return new CurrentExchangeRateResponse
        {
            Today = current.Today,
            IsMissing = current.IsMissing,
            Latest = current.Latest?.MapToResponse()
        };
    }

    public static GetAllExchangeRatesOptions MapToOptions(this GetAllExchangeRatesRequest request)
    {
        return new GetAllExchangeRatesOptions
        {
            From = request.From,
            To = request.To,
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    // ---------- Categories ----------

    public static Category MapToCategory(this CreateCategoryRequest request)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            ParentId = request.ParentId,
            Name = request.Name.Trim(),
            CodePrefix = BlankToNull(request.CodePrefix)
        };
    }

    public static CategoryChanges MapToCategoryChanges(this UpdateCategoryRequest request, Guid id)
    {
        return new CategoryChanges(id, request.Name.Trim(), BlankToNull(request.CodePrefix));
    }

    public static CategoryResponse MapToResponse(this Category category)
    {
        return new CategoryResponse
        {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = category.Name,
            CodePrefix = category.CodePrefix,
            ProductCount = category.ProductCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    public static CategoriesResponse MapToResponse(this IEnumerable<Category> categories)
    {
        return new CategoriesResponse { Items = categories.Select(MapToResponse) };
    }

    // ---------- Suppliers ----------

    public static Supplier MapToSupplier(this CreateSupplierRequest request)
    {
        return new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            ContactName = BlankToNull(request.ContactName),
            Phone = BlankToNull(request.Phone),
            Notes = BlankToNull(request.Notes),
            IsActive = true
        };
    }

    public static Supplier MapToSupplier(this UpdateSupplierRequest request, Guid id)
    {
        return new Supplier
        {
            Id = id,
            Name = request.Name.Trim(),
            ContactName = BlankToNull(request.ContactName),
            Phone = BlankToNull(request.Phone),
            Notes = BlankToNull(request.Notes),
            IsActive = request.IsActive
        };
    }

    public static SupplierResponse MapToResponse(this Supplier supplier)
    {
        return new SupplierResponse
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactName = supplier.ContactName,
            Phone = supplier.Phone,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            UpdatedAt = supplier.UpdatedAt
        };
    }

    public static SuppliersResponse MapToResponse(this IEnumerable<Supplier> suppliers)
    {
        return new SuppliersResponse { Items = suppliers.Select(MapToResponse) };
    }

    // ---------- Products ----------

    public static Product MapToProduct(this CreateProductRequest request)
    {
        // A blank code stays "" here: the service generates it from the category prefix.
        return request.MapToProduct(Guid.NewGuid(), request.Code ?? "", isActive: true);
    }

    public static Product MapToProduct(this UpdateProductRequest request, Guid id)
    {
        return request.MapToProduct(id, request.Code, request.IsActive);
    }

    private static Product MapToProduct(this ProductRequestBase request, Guid id, string code, bool isActive)
    {
        return new Product
        {
            Id = id,
            Code = code.Trim().ToUpperInvariant(),
            Barcode = BlankToNull(request.Barcode),
            Name = request.Name.Trim(),
            Brand = BlankToNull(request.Brand),
            Presentation = BlankToNull(request.Presentation),
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            UsualSupplierId = request.UsualSupplierId,
            PurchaseUnit = request.PurchaseUnit.ToModel(),
            StockUnit = request.StockUnit.ToModel(),
            ConversionFactorMilli = request.ConversionFactorMilli,
            SalePriceCents = request.SalePriceCents,
            MinStockMilli = request.MinStockMilli,
            ShrinkageToleranceBps = request.ShrinkageToleranceBps,
            ExchangeBufferCents = request.ExchangeBufferCents,
            MarkupBps = request.MarkupBps,
            IsPerishable = request.IsPerishable,
            ShelfLifeDays = request.ShelfLifeDays,
            IsActive = isActive,
            Notes = BlankToNull(request.Notes)
        };
    }

    public static ProductResponse MapToResponse(this Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Code = product.Code,
            Barcode = product.Barcode,
            Name = product.Name,
            Brand = product.Brand,
            Presentation = product.Presentation,
            CategoryId = product.CategoryId,
            SubcategoryId = product.SubcategoryId,
            UsualSupplierId = product.UsualSupplierId,
            PurchaseUnit = product.PurchaseUnit.ToContract(),
            StockUnit = product.StockUnit.ToContract(),
            ConversionFactorMilli = product.ConversionFactorMilli,
            AllowsDecimals = product.AllowsDecimals,
            SalePriceCents = product.SalePriceCents,
            IsPriceChangedSinceListed = product.SalePriceCents != product.ListedPriceCents,
            MinStockMilli = product.MinStockMilli,
            ShrinkageToleranceBps = product.ShrinkageToleranceBps,
            ExchangeBufferCents = product.ExchangeBufferCents,
            MarkupBps = product.MarkupBps,
            IsPerishable = product.IsPerishable,
            ShelfLifeDays = product.ShelfLifeDays,
            IsActive = product.IsActive,
            Notes = product.Notes,
            StockMilli = product.StockMilli,
            LastPurchaseDate = product.LastPurchaseDate,
            Cost = product.Pricing?.MapToResponse(),
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public static ProductsResponse MapToResponse(this IEnumerable<Product> products,
        int page, int pageSize, int total)
    {
        return new ProductsResponse
        {
            Items = products.Select(MapToResponse),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static GetAllProductsOptions MapToOptions(this GetAllProductsRequest request)
    {
        return new GetAllProductsOptions
        {
            Search = request.Search,
            CategoryId = request.CategoryId,
            IncludeInactive = request.IncludeInactive.GetValueOrDefault(),
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    public static PriceHistoryResponse MapToResponse(this IEnumerable<ProductPriceChange> changes)
    {
        return new PriceHistoryResponse
        {
            Items = changes.Select(change => new PriceChangeResponse
            {
                Id = change.Id,
                PriceCents = change.PriceCents,
                PreviousPriceCents = change.PreviousPriceCents,
                EffectiveDate = change.EffectiveDate,
                Reason = change.Reason.ToContract(),
                CreatedAt = change.CreatedAt
            })
        };
    }

    public static UnitsResponse MapToUnitsResponse(this IEnumerable<MeasureUnit> units)
    {
        return new UnitsResponse
        {
            Items = units.Select(unit => new UnitResponse
            {
                Code = unit.ToContract(),
                AllowsDecimals = unit.AllowsDecimals()
            })
        };
    }

    // ---------- Price list ----------

    public static PriceListCopyResponse MapToResponse(this PriceListCopy copy)
    {
        return new PriceListCopyResponse
        {
            Id = copy.Id,
            CopiedAt = copy.CopiedAt,
            ProductCount = copy.ProductCount,
            ChangedCount = copy.ChangedCount
        };
    }

    /// <summary>The repository already sorts by category, so grouping keeps that order.</summary>
    public static PriceListResponse MapToResponse(this PriceList priceList)
    {
        return new PriceListResponse
        {
            LastCopy = priceList.LastCopy?.MapToResponse(),
            ChangedCount = priceList.ChangedCount,
            Groups = priceList.Entries
                .GroupBy(entry => new { entry.CategoryId, entry.CategoryName })
                .Select(group => new PriceListGroupResponse
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    Entries = group.Select(entry => new PriceListEntryResponse
                    {
                        ProductId = entry.ProductId,
                        Code = entry.Code,
                        Name = entry.Name,
                        Brand = entry.Brand,
                        Presentation = entry.Presentation,
                        StockUnit = entry.StockUnit.ToContract(),
                        SubcategoryName = entry.SubcategoryName,
                        SalePriceCents = entry.SalePriceCents,
                        ListedPriceCents = entry.ListedPriceCents,
                        IsChanged = entry.IsChanged
                    }).ToList()
                })
                .ToList()
        };
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ---------- Enums ----------
    // Inbound: an undefined value (a client can send "currency": 5) is passed through as-is so the
    // validator's IsInEnum rejects it with a 400, instead of a 500 here.
    // Outbound: a model value with no wire equivalent is a bug, so it fails loudly.

    private static Currency ToModel(this CurrencyCode code) => code switch
    {
        CurrencyCode.CUP => Currency.CUP,
        CurrencyCode.USD => Currency.USD,
        _ => (Currency)(int)code
    };

    private static CurrencyCode ToContract(this Currency currency) => currency switch
    {
        Currency.CUP => CurrencyCode.CUP,
        Currency.USD => CurrencyCode.USD,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };

    private static CashBoxKind ToModel(this CashBoxType type) => type switch
    {
        CashBoxType.Cash => CashBoxKind.Cash,
        CashBoxType.Bank => CashBoxKind.Bank,
        _ => (CashBoxKind)(int)type
    };

    private static CashBoxType ToContract(this CashBoxKind kind) => kind switch
    {
        CashBoxKind.Cash => CashBoxType.Cash,
        CashBoxKind.Bank => CashBoxType.Bank,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static MeasureUnit ToModel(this UnitCode code) => code switch
    {
        UnitCode.Each => MeasureUnit.Each,
        UnitCode.Pound => MeasureUnit.Pound,
        UnitCode.Kilogram => MeasureUnit.Kilogram,
        UnitCode.Liter => MeasureUnit.Liter,
        UnitCode.Sack => MeasureUnit.Sack,
        UnitCode.Box => MeasureUnit.Box,
        UnitCode.Pack => MeasureUnit.Pack,
        UnitCode.Dozen => MeasureUnit.Dozen,
        UnitCode.Bag => MeasureUnit.Bag,
        _ => (MeasureUnit)(int)code
    };

    private static UnitCode ToContract(this MeasureUnit unit) => unit switch
    {
        MeasureUnit.Each => UnitCode.Each,
        MeasureUnit.Pound => UnitCode.Pound,
        MeasureUnit.Kilogram => UnitCode.Kilogram,
        MeasureUnit.Liter => UnitCode.Liter,
        MeasureUnit.Sack => UnitCode.Sack,
        MeasureUnit.Box => UnitCode.Box,
        MeasureUnit.Pack => UnitCode.Pack,
        MeasureUnit.Dozen => UnitCode.Dozen,
        MeasureUnit.Bag => UnitCode.Bag,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    private static PriceChangeKind ToContract(this PriceChangeReason reason) => reason switch
    {
        PriceChangeReason.Manual => PriceChangeKind.Manual,
        PriceChangeReason.Purchase => PriceChangeKind.Purchase,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };
}
