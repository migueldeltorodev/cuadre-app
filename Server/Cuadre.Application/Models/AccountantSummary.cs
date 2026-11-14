namespace Cuadre.Application.Models;

// D-63: the month as the accountant needs it (estado de rendimiento + totals). Read-only, from confirmed documents.
// Money in CUP cents; what was paid in USD is kept apart AND converted to CUP at the rate of its own day.

public static class Exchange
{
    /// <summary>USD cents × CUP cents per USD → CUP cents. 10.00 USD at 325.00 = 3,250.00 CUP.</summary>
    public static long ToCupCents(long usdCents, long rateCupPerUsdCents) =>
        Units.RoundToLong(usdCents * (decimal)rateCupPerUsdCents / 100m);
}

/// <summary>What came in with one payment method (the closings only collect CUP, D-28).</summary>
public sealed record IncomeByMethod(Guid PaymentMethodId, string Name, bool IsTransfer, long AmountCents);

/// <summary>The cost of what was sold, from the closings: A2 = real FIFO cost + colchón (D-60).</summary>
public class CostOfSalesSplit
{
    /// <summary>A2 as recorded.</summary>
    public long CostCents { get; init; }

    /// <summary>The colchón part of A2: money kept for Reposición, not a cost.</summary>
    public long BufferCents { get; init; }

    public long RealCents => CostCents - BufferCents;
}

/// <summary>One confirmed expense, with the rate of its day (or the last one before it) when it was paid in USD.</summary>
public class ExpenseLine
{
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public DateOnly Date { get; init; }
    public Currency Currency { get; init; }
    public long AmountCents { get; init; }

    /// <summary>Null when no rate was ever typed on or before that day.</summary>
    public long? RateCents { get; init; }
}

/// <summary>The expenses of one category: each currency apart, and the USD ones also in CUP.</summary>
public sealed record ConvertedExpenseTotal(
    Guid CategoryId,
    string CategoryName,
    long CupCents,
    long UsdCents,
    long UsdAsCupCents,
    long UsdWithoutRateCents)
{
    /// <summary>What the category weighs in CUP (USD without a rate cannot be converted: it is left out).</summary>
    public long TotalCupCents => CupCents + UsdAsCupCents;

    public static List<ConvertedExpenseTotal> Summarize(IEnumerable<ExpenseLine> lines) => lines
        .GroupBy(line => line.CategoryId)
        .Select(group =>
        {
            var usd = group.Where(line => line.Currency == Currency.USD).ToList();
            return new ConvertedExpenseTotal(
                group.Key,
                group.First().CategoryName,
                group.Where(line => line.Currency == Currency.CUP).Sum(line => line.AmountCents),
                usd.Sum(line => line.AmountCents),
                usd.Where(line => line.RateCents is not null)
                    .Sum(line => Exchange.ToCupCents(line.AmountCents, line.RateCents!.Value)),
                usd.Where(line => line.RateCents is null).Sum(line => line.AmountCents));
        })
        .OrderByDescending(total => total.TotalCupCents)
        .ThenBy(total => total.CategoryName)
        .ToList();
}

/// <summary>An amount of a confirmed purchase (its goods, or one of its charges) with the purchase's own rate.</summary>
public class PurchaseAmountRow
{
    public Guid PurchaseId { get; init; }
    public Currency Currency { get; init; }
    public long AmountCents { get; init; }
    public long RateCents { get; init; }
}

/// <summary>What was bought in the period (goods + charges): each currency apart, the USD also in CUP.</summary>
public sealed record PurchaseTotals(int Count, long CupCents, long UsdCents, long UsdAsCupCents)
{
    public long TotalCupCents => CupCents + UsdAsCupCents;

    /// <summary>USD at the rate of each purchase: the same one its cost was fixed with (D-60).</summary>
    public static PurchaseTotals Summarize(IReadOnlyCollection<PurchaseAmountRow> rows)
    {
        var usd = rows.Where(row => row.Currency == Currency.USD).ToList();
        return new PurchaseTotals(
            rows.Select(row => row.PurchaseId).Distinct().Count(),
            rows.Where(row => row.Currency == Currency.CUP).Sum(row => row.AmountCents),
            usd.Sum(row => row.AmountCents),
            usd.Sum(row => Exchange.ToCupCents(row.AmountCents, row.RateCents)));
    }
}

/// <summary>The quantity of a product in a location on a day.</summary>
public class InventoryRow
{
    public Guid ProductId { get; init; }
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public long QuantityMilli { get; init; }
}

/// <summary>The goods of a location on a day, at what they cost (FIFO, without colchón).</summary>
public sealed record LocationValue(Guid LocationId, string LocationName, long ValueCents);

public static class InventoryValuation
{
    /// <summary>
    /// The batches belong to the product, not to the location (D-60): each location takes its share of the
    /// product's value by quantity. The last location of a product takes the rounding, so the locations add up
    /// to exactly what the batches are worth. A product with no stock left (or only pending units) is worth 0.
    /// </summary>
    public static List<LocationValue> ByLocation(IEnumerable<InventoryRow> rows,
        IReadOnlyDictionary<Guid, FifoBook> books)
    {
        var values = new Dictionary<Guid, (string Name, long Cents)>();

        foreach (var product in rows.GroupBy(row => row.ProductId))
        {
            var places = product.ToList();
            var book = books.GetValueOrDefault(product.Key);
            var stockMilli = book?.StockMilli ?? 0;
            var totalCents = book?.StockCupCents ?? 0;
            long given = 0;

            for (var i = 0; i < places.Count; i++)
            {
                var place = places[i];
                long cents = 0;
                if (stockMilli > 0 && totalCents != 0)
                {
                    cents = i == places.Count - 1
                        ? totalCents - given
                        : Units.RoundToLong(place.QuantityMilli * (decimal)totalCents / stockMilli);
                }

                given += cents;
                var before = values.TryGetValue(place.LocationId, out var current) ? current.Cents : 0;
                values[place.LocationId] = (place.LocationName, before + cents);
            }
        }

        return values
            .Select(pair => new LocationValue(pair.Key, pair.Value.Name, pair.Value.Cents))
            .OrderBy(value => value.LocationName)
            .ToList();
    }
}

/// <summary>The goods of a location at the start (the day before the 1st) and at the end of the period.</summary>
public sealed record LocationInventory(Guid LocationId, string LocationName, long OpeningCents, long ClosingCents)
{
    public static List<LocationInventory> Join(IEnumerable<LocationValue> opening, IEnumerable<LocationValue> closing)
    {
        var start = opening.ToDictionary(value => value.LocationId);
        var end = closing.ToDictionary(value => value.LocationId);
        return start.Keys.Union(end.Keys)
            .Select(id =>
            {
                var name = end.GetValueOrDefault(id)?.LocationName ?? start[id].LocationName;
                return new LocationInventory(id, name, start.GetValueOrDefault(id)?.ValueCents ?? 0,
                    end.GetValueOrDefault(id)?.ValueCents ?? 0);
            })
            .OrderBy(value => value.LocationName)
            .ToList();
    }
}

/// <summary>
/// D-63: the estado de rendimiento of a month.
/// Income − cost of sales (REAL FIFO cost: the colchón is money kept, not a cost) = gross profit;
/// − expenses − staff pay − merchandise losses + count gains = profit before taxes; − suggested taxes = after.
/// Cash shortages are NOT subtracted: the income is what was really collected, so they are already missing from it.
/// </summary>
public sealed record AccountantSummary(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    int ClosedDays,
    IReadOnlyList<IncomeByMethod> Income,
    long CostOfSalesCents,
    long BufferCents,
    IReadOnlyList<ConvertedExpenseTotal> Expenses,
    long StaffPayCents,
    long MerchandiseLossCents,
    long CountGainCents,
    long LostShortageCents,
    IReadOnlyList<TaxSuggestion> Taxes,
    PurchaseTotals Purchases,
    IReadOnlyList<LocationInventory> Inventory)
{
    public long IncomeCents => Income.Sum(method => method.AmountCents);
    public long GrossProfitCents => IncomeCents - CostOfSalesCents;
    public long ExpensesCents => Expenses.Sum(total => total.TotalCupCents);

    public long ProfitBeforeTaxesCents =>
        ProfitBeforeTaxes(IncomeCents, CostOfSalesCents, ExpensesCents, StaffPayCents, MerchandiseLossCents,
            CountGainCents);

    public long TaxesCents => Taxes.Sum(tax => tax.AmountCents);
    public long ProfitAfterTaxesCents => ProfitBeforeTaxesCents - TaxesCents;

    /// <summary>USD expenses with no rate on or before their day: left out of the CUP totals until one is typed.</summary>
    public long UsdWithoutRateCents => Expenses.Sum(total => total.UsdWithoutRateCents);

    public long OpeningInventoryCents => Inventory.Sum(location => location.OpeningCents);
    public long ClosingInventoryCents => Inventory.Sum(location => location.ClosingCents);

    public static AccountantSummary Create(int year, int month, DateOnly from, DateOnly to, int closedDays,
        IReadOnlyList<IncomeByMethod> income, CostOfSalesSplit cost, IReadOnlyList<ConvertedExpenseTotal> expenses,
        long staffPayCents, long merchandiseLossCents, long countGainCents, long lostShortageCents,
        IEnumerable<Tax> taxes, PurchaseTotals purchases, IReadOnlyList<LocationInventory> inventory)
    {
        var incomeCents = income.Sum(method => method.AmountCents);
        var before = ProfitBeforeTaxes(incomeCents, cost.RealCents, expenses.Sum(total => total.TotalCupCents),
            staffPayCents, merchandiseLossCents, countGainCents);

        return new AccountantSummary(year, month, from, to, closedDays, income, cost.RealCents, cost.BufferCents,
            expenses, staffPayCents, merchandiseLossCents, countGainCents, lostShortageCents,
            TaxSuggestion.For(taxes, incomeCents, before), purchases, inventory);
    }

    private static long ProfitBeforeTaxes(long incomeCents, long costOfSalesCents, long expensesCents,
        long staffPayCents, long merchandiseLossCents, long countGainCents) =>
        incomeCents - costOfSalesCents - expensesCents - staffPayCents - merchandiseLossCents + countGainCents;
}
