namespace Cuadre.Application.Models;

/// <summary>
/// The on-screen price list to copy by hand to the shop sign (RF-PRD-09). Active products only,
/// unpriced ones included (the UI flags them).
/// </summary>
public sealed record PriceList(PriceListCopy? LastCopy, IReadOnlyList<PriceListEntry> Entries)
{
    public int ChangedCount => Entries.Count(e => e.IsChanged);
}

public sealed class PriceListEntry
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Brand { get; init; }
    public string? Presentation { get; init; }
    public MeasureUnit StockUnit { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public Guid? SubcategoryId { get; init; }
    public string? SubcategoryName { get; init; }
    public long? SalePriceCents { get; init; }
    public long? ListedPriceCents { get; init; }

    /// <summary>Price differs from the one on the sign (or the product was never on it).</summary>
    public bool IsChanged => SalePriceCents != ListedPriceCents;
}

/// <summary>
/// A class, not a positional record, on purpose: Dapper materializes records through their constructor,
/// and that path skips our Guid/DateTime type handlers (TEXT columns would fail to bind).
/// </summary>
public sealed class PriceListCopy
{
    public required Guid Id { get; init; }
    public required DateTime CopiedAt { get; init; }
    public required int ProductCount { get; init; }
    public required int ChangedCount { get; init; }
}
