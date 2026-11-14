namespace Cuadre.Application.Models;

// D-60: the cost of what leaves the shelf is taken FIFO, batch by batch ("tandas"). Pure math, unit tested.
// A batch is what one purchase line brought in. Batches belong to the PRODUCT, not to a location: a bottle in the
// storage room and one on the counter from the same purchase are the same batch, so transfers never touch them.

/// <summary>A stock movement as the FIFO replay sees it.</summary>
public sealed record FifoMovement(
    Guid Id,
    DateOnly Date,
    StockMovementKind Kind,
    long QuantityMilli,
    long? UnitCostUsdMicros,
    long? UnitCostCupMicros,
    Guid? PurchaseLineId);

/// <summary>One batch on the shelf: what is left of it and what each unit cost (USD and CUP at its purchase rate).</summary>
public sealed class FifoLayer
{
    internal FifoLayer(Guid? purchaseLineId, DateOnly date, long quantityMilli, UnitCost? cost)
    {
        PurchaseLineId = purchaseLineId;
        Date = date;
        QuantityMilli = quantityMilli;
        RemainingMilli = quantityMilli;
        Cost = cost;
    }

    /// <summary>The purchase line that brought it. Null for a count surplus or a negative sale.</summary>
    public Guid? PurchaseLineId { get; }

    public DateOnly Date { get; }
    public long QuantityMilli { get; }
    public long RemainingMilli { get; internal set; }

    /// <summary>Null only before the product was ever bought (it takes the first purchase's cost when it comes).</summary>
    public UnitCost? Cost { get; internal set; }
}

/// <summary>
/// What one outflow took from the batches. The cost is kept exact (Σ quantity × unit cost) and rounded only when
/// read. Part of it may still be PENDING: taken beyond the stock (the purchase was not typed in yet) or before the
/// product had any cost. The replay fills it when the next batch comes; until then it is valued at an estimate.
/// </summary>
public sealed class FifoTake
{
    private const decimal MilliMicrosPerCent = 10_000_000m;     // 1,000 (milli) × 1,000,000 (micros) / 100
    private const decimal MilliMicrosPerUnit = 1_000_000_000m;  // 1,000 (milli) × 1,000,000 (micros)

    private decimal _usd;
    private decimal _cup;

    internal FifoTake(Guid? movementId, DateOnly date, long quantityMilli)
    {
        MovementId = movementId;
        Date = date;
        QuantityMilli = quantityMilli;
    }

    public Guid? MovementId { get; }
    public DateOnly Date { get; }
    public long QuantityMilli { get; }

    /// <summary>Taken beyond the stock: waiting for the next entry.</summary>
    public long ShortMilli { get; internal set; }

    /// <summary>Taken from stock that had no cost yet (never bought).</summary>
    public long UncostedMilli { get; internal set; }

    public long PendingMilli => ShortMilli + UncostedMilli;

    internal void Add(long quantityMilli, UnitCost cost)
    {
        _usd += quantityMilli * (decimal)cost.UsdMicros;
        _cup += quantityMilli * (decimal)cost.CupMicros;
    }

    /// <summary>Real CUP cost, in cents: what these units cost at their purchase rates. Pending at the estimate.</summary>
    public long CupCents(UnitCost? estimate) =>
        Units.RoundToLong((_cup + PendingMilli * (decimal)(estimate?.CupMicros ?? 0)) / MilliMicrosPerCent);

    /// <summary>The colchón part of A2: USD cost × colchón (CUP cents per USD), in cents.</summary>
    public long BufferCents(UnitCost? estimate, long bufferCents) =>
        Units.RoundToLong((_usd + PendingMilli * (decimal)(estimate?.UsdMicros ?? 0)) * bufferCents
                          / MilliMicrosPerUnit);

    /// <summary>The blended cost per unit of what was taken. Null when nothing of it has a cost.</summary>
    public UnitCost? UnitCost(UnitCost? estimate)
    {
        var usd = _usd + PendingMilli * (decimal)(estimate?.UsdMicros ?? 0);
        var cup = _cup + PendingMilli * (decimal)(estimate?.CupMicros ?? 0);
        if (QuantityMilli <= 0 || usd <= 0 || cup <= 0)
        {
            return null;
        }

        return new UnitCost(Math.Max(1, Units.RoundToLong(usd / QuantityMilli)),
            Math.Max(1, Units.RoundToLong(cup / QuantityMilli)));
    }
}

/// <summary>
/// The batches of ONE product, built by replaying its stock ledger in order (date, then creation). Voided documents
/// are left out of the replay entirely, so voiding or reopening needs no special case: replay again and that's it.
/// Rules (D-60):
///   - An entry with a cost (a purchase) is a new batch.
///   - An entry WITHOUT a cost (a count surplus, a negative sale) is a batch at the cost of the NEWEST batch.
///   - Every outflow (sale, exit, count shortage) takes from the OLDEST batch first.
///   - Transfers are ignored: batches are per product.
///   - Taken beyond the stock: that part waits for the next entry, which pays it first.
///   - Stock and outflows before the first purchase take the first purchase's cost when it comes.
/// </summary>
public sealed class FifoBook
{
    private readonly List<FifoLayer> _layers = [];
    private readonly List<FifoTake> _takes = [];
    private readonly List<ShortPiece> _short = [];
    private readonly List<(FifoTake Take, long QuantityMilli)> _uncosted = [];
    private int _head;

    /// <summary>The newest batch's cost (the last purchase). Null if never bought.</summary>
    public UnitCost? NewestCost { get; private set; }

    /// <summary>The outflows of the replay, oldest first, each with what it took.</summary>
    public IReadOnlyList<FifoTake> Takes => _takes;

    /// <summary>The batches still on the shelf, oldest first.</summary>
    public IReadOnlyList<FifoLayer> Layers => _layers.Skip(_head).Where(layer => layer.RemainingMilli > 0).ToList();

    /// <summary>The batch being sold now: the oldest one with stock. Null when the shelf is empty.</summary>
    public FifoLayer? CurrentLayer => Layers.FirstOrDefault();

    /// <summary>The cost of the batch being sold now; with an empty shelf, the newest one (the next to come).</summary>
    public UnitCost? CurrentCost => CurrentLayer?.Cost ?? NewestCost;

    public long StockMilli => _layers.Skip(_head).Sum(layer => layer.RemainingMilli) - _short.Sum(p => p.QuantityMilli);

    /// <summary>What is on the shelf at what it cost, in CUP cents (stock without a cost counts as 0).</summary>
    public long StockCupCents => Units.RoundToLong(Layers.Sum(layer =>
        layer.RemainingMilli * (decimal)(layer.Cost?.CupMicros ?? 0)) / 10_000_000m);

    public static FifoBook Replay(IEnumerable<FifoMovement> movements)
    {
        var book = new FifoBook();
        foreach (var movement in movements)
        {
            book.Apply(movement);
        }

        return book;
    }

    public void Apply(FifoMovement movement)
    {
        if (movement.Kind is StockMovementKind.TransferIn or StockMovementKind.TransferOut
            || movement.QuantityMilli == 0)
        {
            return;
        }

        if (movement.QuantityMilli > 0)
        {
            var cost = movement is { UnitCostUsdMicros: { } usd, UnitCostCupMicros: { } cup }
                ? new UnitCost(usd, cup)
                : null;
            Put(movement.QuantityMilli, cost, movement.PurchaseLineId, movement.Date);
        }
        else
        {
            _takes.Add(Take(-movement.QuantityMilli, movement.Date, movement.Id));
        }
    }

    /// <summary>An entry. Without a cost it enters at the newest batch's cost.</summary>
    public void Put(long quantityMilli, UnitCost? cost, Guid? purchaseLineId, DateOnly date)
    {
        if (quantityMilli <= 0)
        {
            return;
        }

        if (cost is not null)
        {
            NewestCost = cost;
            CostTheUncosted(cost);
        }

        var effective = cost ?? NewestCost;
        var left = quantityMilli;

        // What was taken beyond the stock came, in fact, from this entry.
        while (left > 0 && _short.Count > 0)
        {
            var piece = _short[0];
            var used = Math.Min(left, piece.QuantityMilli);
            piece.Take.ShortMilli -= used;
            if (effective is not null)
            {
                piece.Take.Add(used, effective);
            }
            else
            {
                piece.Take.UncostedMilli += used;
                _uncosted.Add((piece.Take, used));
            }

            piece.QuantityMilli -= used;
            left -= used;
            if (piece.QuantityMilli == 0)
            {
                _short.RemoveAt(0);
            }
        }

        if (left > 0)
        {
            _layers.Add(new FifoLayer(purchaseLineId, date, left, effective));
        }
    }

    /// <summary>An outflow: oldest batch first. Returns what it took (also used to cost a closing before saving it).</summary>
    public FifoTake Take(long quantityMilli, DateOnly date, Guid? movementId = null)
    {
        var take = new FifoTake(movementId, date, quantityMilli);
        var left = quantityMilli;

        while (left > 0 && _head < _layers.Count)
        {
            var layer = _layers[_head];
            var used = Math.Min(left, layer.RemainingMilli);
            if (used > 0)
            {
                if (layer.Cost is { } cost)
                {
                    take.Add(used, cost);
                }
                else
                {
                    take.UncostedMilli += used;
                    _uncosted.Add((take, used));
                }

                layer.RemainingMilli -= used;
                left -= used;
            }

            if (layer.RemainingMilli == 0)
            {
                _head++;
            }
        }

        if (left > 0)
        {
            take.ShortMilli += left;
            _short.Add(new ShortPiece(take, left));
        }

        return take;
    }

    /// <summary>The first purchase arrived: what was there (or went out) before it takes its cost.</summary>
    private void CostTheUncosted(UnitCost cost)
    {
        foreach (var layer in _layers.Where(layer => layer.Cost is null))
        {
            layer.Cost = cost;
        }

        foreach (var (take, quantity) in _uncosted)
        {
            take.UncostedMilli -= quantity;
            take.Add(quantity, cost);
        }

        _uncosted.Clear();
    }

    private sealed class ShortPiece(FifoTake take, long quantityMilli)
    {
        public FifoTake Take { get; } = take;
        public long QuantityMilli { get; set; } = quantityMilli;
    }
}

/// <summary>What one counted line of a closing costs (RN-07 with D-60): real FIFO cost plus the colchón.</summary>
public sealed record SaleCost(UnitCost? UnitCost, long RealCents, long BufferCents)
{
    /// <summary>A2 of the line = what it cost + USD cost × colchón (Reposición keeps the colchón).</summary>
    public long CostCents => RealCents + BufferCents;
}

public static class FifoCosting
{
    /// <summary>
    /// A2 of one counted line, and the book moves with it (several lines of a product are taken in order).
    /// Sold &gt; 0: taken FIFO. Sold &lt; 0 (more on the counter than expected): it goes back on the shelf at the
    /// newest batch's cost, and A2 goes down by the same.
    /// </summary>
    public static SaleCost Sale(FifoBook book, long soldMilli, long bufferCents, DateOnly date)
    {
        if (soldMilli > 0)
        {
            var take = book.Take(soldMilli, date);
            return new SaleCost(take.UnitCost(book.NewestCost), take.CupCents(book.NewestCost),
                take.BufferCents(book.NewestCost, bufferCents));
        }

        if (soldMilli < 0)
        {
            var cost = book.NewestCost;
            book.Put(-soldMilli, null, null, date);
            return cost is null
                ? new SaleCost(null, 0, 0)
                : new SaleCost(cost, Units.RoundToLong(soldMilli * (decimal)cost.CupMicros / 10_000_000m),
                    Units.RoundToLong(soldMilli * (decimal)cost.UsdMicros * bufferCents / 1_000_000_000m));
        }

        return new SaleCost(null, 0, 0);
    }

    /// <summary>A unit cost with the colchón folded into the CUP side: USD × (purchase rate + colchón).</summary>
    public static UnitCost WithBuffer(UnitCost cost, long bufferCents) =>
        new(cost.UsdMicros, cost.CupMicros + Units.RoundToLong(cost.UsdMicros * (decimal)bufferCents / 100m));
}
