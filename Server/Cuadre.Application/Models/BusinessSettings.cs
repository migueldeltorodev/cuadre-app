namespace Cuadre.Application.Models;

/// <summary>
/// Single-row business configuration (RF-CFG-01/02/05/06/07).
/// Money in CUP cents, percentages in basis points (1% = 100 bps).
/// </summary>
public class BusinessSettings
{
    public required string Name { get; set; }
    public string? Address { get; set; }

    /// <summary>Days with no daily close (e.g. Sunday). Never all seven.</summary>
    public IReadOnlyCollection<DayOfWeek> ClosedDays { get; set; } = [];

    /// <summary>A cash difference above this (absolute value) requires a note at the close (RF-CFG-05).</summary>
    public long CashDifferenceThresholdCents { get; set; }

    /// <summary>"Colchón": fixed CUP added to the purchase rate when pricing (D-07, D-08).</summary>
    public long ExchangeBufferCents { get; set; }

    /// <summary>Default "recargo" over cost when a product has none of its own (D-07).</summary>
    public int DefaultMarkupBps { get; set; }

    /// <summary>Suggested prices are rounded to a multiple of this. 100 = whole pesos.</summary>
    public long PriceRoundingStepCents { get; set; }

    public DateTime UpdatedAt { get; set; }
}
