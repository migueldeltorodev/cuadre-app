using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// D-63: the month as the accountant needs it. Money in CUP cents; amounts paid in USD come apart (UsdCents) and also
// converted to CUP at the rate of their own day (UsdAsCupCents).

public class IncomeByMethodResponse
{
    public required Guid PaymentMethodId { get; init; }
    public required string Name { get; init; }

    /// <summary>It landed in a bank box.</summary>
    public required bool IsTransfer { get; init; }

    public required long AmountCents { get; init; }
}

public class ConvertedExpenseTotalResponse
{
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required long CupCents { get; init; }
    public required long UsdCents { get; init; }

    /// <summary>The USD ones at the rate of their day (or the last one typed before it).</summary>
    public required long UsdAsCupCents { get; init; }

    /// <summary>USD with no rate on or before their day: not in TotalCupCents until one is typed.</summary>
    public required long UsdWithoutRateCents { get; init; }

    public required long TotalCupCents { get; init; }
}

public class TaxSuggestionResponse
{
    public required Guid TaxId { get; init; }
    public required string Name { get; init; }
    public required int RateBps { get; init; }
    public required TaxBaseCode Base { get; init; }

    /// <summary>What it was calculated over: the income, or the profit before taxes (never below 0).</summary>
    public required long BaseCents { get; init; }

    public required long AmountCents { get; init; }
}

/// <summary>Goods + charges of the confirmed purchases. USD at the rate of each purchase.</summary>
public class PurchaseTotalsResponse
{
    public required int Count { get; init; }
    public required long CupCents { get; init; }
    public required long UsdCents { get; init; }
    public required long UsdAsCupCents { get; init; }
    public required long TotalCupCents { get; init; }
}

/// <summary>The goods of a location at FIFO cost (no colchón): the day before the 1st, and the last day.</summary>
public class LocationInventoryResponse
{
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public required long OpeningCents { get; init; }
    public required long ClosingCents { get; init; }
}

/// <summary>
/// The estado de rendimiento: income − cost of sales (real FIFO) = gross profit; − expenses − staff − losses + count
/// gains = profit before taxes; − suggested taxes = after taxes. Lost cash shortages are informative only: the income
/// is what was really collected, so they are already missing from it.
/// </summary>
public class AccountantSummaryResponse
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required int ClosedDays { get; init; }

    public required IReadOnlyList<IncomeByMethodResponse> Income { get; init; } = [];
    public required long IncomeCents { get; init; }

    /// <summary>Real FIFO cost of what was sold, without the colchón.</summary>
    public required long CostOfSalesCents { get; init; }

    /// <summary>The colchón charged in the prices and kept in Reposición (not a cost).</summary>
    public required long BufferCents { get; init; }

    public required long GrossProfitCents { get; init; }

    public required IReadOnlyList<ConvertedExpenseTotalResponse> Expenses { get; init; } = [];
    public required long ExpensesCents { get; init; }
    public required long UsdWithoutRateCents { get; init; }
    public required long StaffPayCents { get; init; }
    public required long MerchandiseLossCents { get; init; }
    public required long CountGainCents { get; init; }
    public required long LostShortageCents { get; init; }
    public required long ProfitBeforeTaxesCents { get; init; }

    /// <summary>Only the active taxes. A suggestion of what to set aside: nothing is moved.</summary>
    public required IReadOnlyList<TaxSuggestionResponse> Taxes { get; init; } = [];

    public required long TaxesCents { get; init; }
    public required long ProfitAfterTaxesCents { get; init; }

    public required PurchaseTotalsResponse Purchases { get; init; }

    public required IReadOnlyList<LocationInventoryResponse> Inventory { get; init; } = [];
    public required long OpeningInventoryCents { get; init; }
    public required long ClosingInventoryCents { get; init; }
}
