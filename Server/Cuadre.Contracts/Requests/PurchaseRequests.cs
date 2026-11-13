using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

// Purchase money in cents of the PURCHASE currency; quantities in thousandths of the PURCHASE unit
// (2 sacos = 2000); the rate in CUP cents per USD (1 USD = 325 CUP → 32500).

public class CreatePurchaseRequest
{
    public required DateOnly Date { get; init; }
    public required Guid SupplierId { get; init; }
    public required CurrencyCode Currency { get; init; }

    /// <summary>The rate the goods were actually paid at. Converts CUP amounts to USD for the cost.</summary>
    public required long RateCupPerUsdCents { get; init; }

    /// <summary>Null = the default storage (Almacén).</summary>
    public Guid? LocationId { get; init; }

    /// <summary>The box the goods were paid from. Same currency as the purchase.</summary>
    public required Guid CashBoxId { get; init; }

    public string? Notes { get; init; }
    public required IReadOnlyList<CreatePurchaseLineRequest> Lines { get; init; } = [];
    public IReadOnlyList<CreatePurchaseChargeRequest> Charges { get; init; } = [];
}

public class CreatePurchaseLineRequest
{
    public required Guid ProductId { get; init; }

    /// <summary>In purchase units, ×1000.</summary>
    public required long QuantityMilli { get; init; }

    /// <summary>Per purchase unit, in the purchase currency.</summary>
    public required long UnitCostCents { get; init; }

    /// <summary>Null = the product's factor. Set when this batch came different (a sack of 95 lb).</summary>
    public long? ConversionFactorMilli { get; init; }
}

/// <summary>Freight, unloading... Spread over the lines by value.</summary>
public class CreatePurchaseChargeRequest
{
    public required string Description { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long AmountCents { get; init; }

    /// <summary>Same currency as the charge.</summary>
    public required Guid CashBoxId { get; init; }
}

public class VoidPurchaseRequest
{
    public required string Reason { get; init; }
}

public class GetAllPurchasesRequest : PagedRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? ProductId { get; init; }

    /// <summary>A category or subcategory: purchases with at least one product in it.</summary>
    public Guid? CategoryId { get; init; }

    public bool? IncludeVoided { get; init; }
}

public class SetSalePriceRequest
{
    public required long SalePriceCents { get; init; }

    /// <summary>Set when accepting a suggestion after that purchase: the history records "Compra".</summary>
    public Guid? PurchaseId { get; init; }
}
