using Cuadre.Application.Models;
using Cuadre.Application.Validators;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 3: purchases, locations, product cost and sale price.</summary>
public static partial class ContractMapping
{
    // ---------- Purchases ----------

    public static Purchase MapToPurchase(this CreatePurchaseRequest request)
    {
        var purchaseId = Guid.NewGuid();
        var purchase = new Purchase
        {
            Id = purchaseId,
            Date = request.Date,
            SupplierId = request.SupplierId,
            Currency = request.Currency.ToModel(),
            RateCupPerUsdCents = request.RateCupPerUsdCents,
            LocationId = request.LocationId ?? Guid.Empty,
            CashBoxId = request.CashBoxId,
            Notes = BlankToNull(request.Notes)
        };

        purchase.Lines.AddRange((request.Lines ?? []).Select(line => new PurchaseLine
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            ProductId = line.ProductId,
            QuantityMilli = line.QuantityMilli,
            UnitCostCents = line.UnitCostCents,
            ConversionFactorMilli = line.ConversionFactorMilli
        }));

        purchase.Charges.AddRange((request.Charges ?? []).Select(charge => new PurchaseCharge
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            Description = charge.Description?.Trim() ?? "",
            Currency = charge.Currency.ToModel(),
            AmountCents = charge.AmountCents,
            CashBoxId = charge.CashBoxId
        }));

        return purchase;
    }

    public static VoidPurchase MapToVoidPurchase(this VoidPurchaseRequest request, Guid id)
    {
        return new VoidPurchase(id, request.Reason ?? "");
    }

    public static PurchaseResponse MapToResponse(this PurchaseResult result)
    {
        return result.Purchase.MapToResponse(result.PriceSuggestions);
    }

    public static PurchaseResponse MapToResponse(this Purchase purchase,
        IEnumerable<PriceSuggestion>? suggestions = null)
    {
        return new PurchaseResponse
        {
            Id = purchase.Id,
            Number = purchase.Number,
            Date = purchase.Date,
            SupplierId = purchase.SupplierId,
            SupplierName = purchase.SupplierName,
            Currency = purchase.Currency.ToContract(),
            RateCupPerUsdCents = purchase.RateCupPerUsdCents,
            LocationId = purchase.LocationId,
            CashBoxId = purchase.CashBoxId,
            GoodsTotalCents = purchase.GoodsTotalCents,
            Notes = purchase.Notes,
            Status = purchase.Status.ToContract(),
            VoidReason = purchase.VoidReason,
            VoidedAt = purchase.VoidedAt,
            Lines = purchase.Lines.Select(line => new PurchaseLineResponse
            {
                Id = line.Id,
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                ProductCode = line.ProductCode,
                PurchaseUnit = line.PurchaseUnit.ToContract(),
                StockUnit = line.StockUnit.ToContract(),
                QuantityMilli = line.QuantityMilli,
                ConversionFactorMilli = line.ConversionFactorMilli ?? 0,
                StockQuantityMilli = line.StockQuantityMilli,
                UnitCostCents = line.UnitCostCents,
                LineTotalCents = line.LineTotalCents,
                AllocatedChargesUsdMicros = line.AllocatedChargesUsdMicros,
                LandedUnitCostUsdMicros = line.LandedUnitCostUsdMicros,
                LandedUnitCostCupMicros = line.LandedUnitCostCupMicros
            }).ToList(),
            Charges = purchase.Charges.Select(charge => new PurchaseChargeResponse
            {
                Id = charge.Id,
                Description = charge.Description,
                Currency = charge.Currency.ToContract(),
                AmountCents = charge.AmountCents,
                CashBoxId = charge.CashBoxId
            }).ToList(),
            PriceSuggestions = (suggestions ?? []).Select(suggestion => new PriceSuggestionResponse
            {
                ProductId = suggestion.ProductId,
                ProductName = suggestion.ProductName,
                CurrentPriceCents = suggestion.CurrentPriceCents,
                SuggestedPriceCents = suggestion.SuggestedPriceCents
            }).ToList(),
            CreatedAt = purchase.CreatedAt,
            UpdatedAt = purchase.UpdatedAt
        };
    }

    public static PurchasesResponse MapToResponse(this IEnumerable<PurchaseSummary> purchases,
        int page, int pageSize, int total)
    {
        return new PurchasesResponse
        {
            Items = purchases.Select(summary => new PurchaseSummaryResponse
            {
                Id = summary.Id,
                Number = summary.Number,
                Date = summary.Date,
                SupplierId = summary.SupplierId,
                SupplierName = summary.SupplierName,
                Currency = summary.Currency.ToContract(),
                GoodsTotalCents = summary.GoodsTotalCents,
                LineCount = summary.LineCount,
                ProductNames = summary.ProductNames,
                Status = summary.Status.ToContract()
            }),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static GetAllPurchasesOptions MapToOptions(this GetAllPurchasesRequest request)
    {
        return new GetAllPurchasesOptions
        {
            From = request.From,
            To = request.To,
            SupplierId = request.SupplierId,
            ProductId = request.ProductId,
            CategoryId = request.CategoryId,
            IncludeVoided = request.IncludeVoided.GetValueOrDefault(),
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    public static ProductPurchasesResponse MapToResponse(this IEnumerable<ProductPurchase> purchases)
    {
        return new ProductPurchasesResponse
        {
            Items = purchases.Select(purchase => new ProductPurchaseResponse
            {
                PurchaseId = purchase.PurchaseId,
                PurchaseNumber = purchase.PurchaseNumber,
                Date = purchase.Date,
                SupplierName = purchase.SupplierName,
                Currency = purchase.Currency.ToContract(),
                RateCupPerUsdCents = purchase.RateCupPerUsdCents,
                QuantityMilli = purchase.QuantityMilli,
                PurchaseUnit = purchase.PurchaseUnit.ToContract(),
                StockQuantityMilli = purchase.StockQuantityMilli,
                StockUnit = purchase.StockUnit.ToContract(),
                UnitCostCents = purchase.UnitCostCents,
                LandedUnitCostUsdMicros = purchase.LandedUnitCostUsdMicros,
                LandedUnitCostCupMicros = purchase.LandedUnitCostCupMicros,
                Status = purchase.Status.ToContract()
            })
        };
    }

    public static SupplierProductsResponse MapToResponse(this IEnumerable<SupplierProduct> products)
    {
        return new SupplierProductsResponse
        {
            Items = products.Select(product => new SupplierProductResponse
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                ProductCode = product.ProductCode,
                StockUnit = product.StockUnit.ToContract(),
                LastPurchaseDate = product.LastPurchaseDate,
                LastLandedUnitCostUsdMicros = product.LastLandedUnitCostUsdMicros,
                PurchaseCount = product.PurchaseCount
            })
        };
    }

    // ---------- Locations ----------

    public static LocationsResponse MapToResponse(this IEnumerable<Location> locations)
    {
        return new LocationsResponse
        {
            Items = locations.Select(location => new LocationResponse
            {
                Id = location.Id,
                Name = location.Name,
                Kind = location.Kind.ToContract(),
                IsActive = location.IsActive
            })
        };
    }

    // ---------- Product cost and sale price ----------

    public static ProductCostResponse MapToResponse(this ProductPricing pricing)
    {
        return new ProductCostResponse
        {
            AvgCostUsdMicros = pricing.AvgCostUsdMicros,
            AvgCostCupMicros = pricing.AvgCostCupMicros,
            LastCostUsdMicros = pricing.LastCostUsdMicros,
            LastCostCupMicros = pricing.LastCostCupMicros,
            LastRateCents = pricing.LastRateCents,
            EffectiveBufferCents = pricing.EffectiveBufferCents,
            EffectiveMarkupBps = pricing.EffectiveMarkupBps,
            SuggestedPriceCents = pricing.SuggestedPriceCents,
            MarginBps = pricing.MarginBps,
            TodayRateCents = pricing.TodayRateCents,
            IsBufferExceeded = pricing.IsBufferExceeded
        };
    }

    public static SetSalePrice MapToSetSalePrice(this SetSalePriceRequest request, Guid productId)
    {
        return new SetSalePrice(productId, request.SalePriceCents, request.PurchaseId);
    }

    // ---------- Enums (outbound only: these never come in) ----------

    private static PurchaseState ToContract(this PurchaseStatus status) => status switch
    {
        PurchaseStatus.Confirmed => PurchaseState.Confirmed,
        PurchaseStatus.Voided => PurchaseState.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static LocationType ToContract(this LocationKind kind) => kind switch
    {
        LocationKind.Storage => LocationType.Storage,
        LocationKind.Counter => LocationType.Counter,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
