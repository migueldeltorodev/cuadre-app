namespace Cuadre.Application.Models;

// D-62: configurable taxes. They never move money: they only suggest how much to set aside.

/// <summary>What a tax is calculated over.</summary>
public enum TaxBase
{
    /// <summary>What was collected in the period (A1).</summary>
    Sales,

    /// <summary>The profit before taxes of the accountant summary (never below 0).</summary>
    Profit
}

public class Tax
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required int RateBps { get; set; }
    public required TaxBase Base { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record TaxChanges(Guid Id, string Name, int RateBps, TaxBase Base, bool IsActive);

/// <summary>How much one active tax suggests setting aside for a period.</summary>
public sealed record TaxSuggestion(Guid TaxId, string Name, int RateBps, TaxBase Base, long BaseCents, long AmountCents)
{
    /// <summary>
    /// Every ACTIVE tax over its base: sales = what was collected; profit = the profit before taxes, 0 when there
    /// was a loss (a loss pays no tax on profit). Each one over the same base: they do not pile up on each other.
    /// </summary>
    public static List<TaxSuggestion> For(IEnumerable<Tax> taxes, long salesCents, long profitBeforeTaxesCents) =>
        taxes
            .Where(tax => tax.IsActive)
            .Select(tax =>
            {
                var baseCents = tax.Base == TaxBase.Sales ? salesCents : Math.Max(0, profitBeforeTaxesCents);
                return new TaxSuggestion(tax.Id, tax.Name, tax.RateBps, tax.Base, baseCents,
                    Units.RoundToLong(baseCents * (decimal)tax.RateBps / 10_000m));
            })
            .ToList();
}
