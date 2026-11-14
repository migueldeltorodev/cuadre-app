using System.Data;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>
/// Rebuilds the product_costs cache of one product by replaying its stock ledger (CostCalculator).
/// Runs INSIDE the caller's transaction, so a purchase/void and its new costs commit (or fail) together.
/// Movements of VOIDED purchases, stock operations and reopened closings are left out entirely (both the original rows and their
/// reversal): the replay then gives exactly the cost the product would have had if that document never happened.
/// Operations matter too: an exit that empties the stock makes the next purchase restart the average.
/// Outgoing rows may carry a cost (exits value the loss with it): the replay ignores it, outflows never move it.
/// The LAST cost (D-52) is the latest entry of a confirmed purchase: positive count differences carry a cost too,
/// but they are not purchases. Without one (all voided), it falls back to the average.
/// </summary>
internal static class ProductCostLedger
{
    public static async Task RecalculateAsync(IDbConnection connection, IDbTransaction transaction,
        Guid productId, DateTime now, CancellationToken cancellationToken)
    {
        var movements = await connection.QueryAsync<CostMovementRow>(new CommandDefinition("""
            SELECT m.quantity_milli, m.unit_cost_usd_micros, m.unit_cost_cup_micros, m.date, m.purchase_id
            FROM stock_movements m
                LEFT JOIN purchases p ON p.id = m.purchase_id
                LEFT JOIN stock_operations o ON o.id = m.operation_id
                LEFT JOIN daily_closings c ON c.id = m.closing_id
            WHERE m.product_id = @productId
              AND (p.id IS NULL OR p.status = 'Confirmed')
              AND (o.id IS NULL OR o.status = 'Confirmed')
              AND (c.id IS NULL OR c.status = 'Confirmed')
            ORDER BY m.date, m.created_at, m.rowid
            """, new { productId }, transaction, cancellationToken: cancellationToken));

        var rows = movements.ToList();
        var cost = CostCalculator.Replay(rows.Select(row =>
            new CostMovement(row.QuantityMilli, row.UnitCostUsdMicros, row.UnitCostCupMicros)));

        if (cost is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM product_costs WHERE product_id = @productId",
                new { productId }, transaction, cancellationToken: cancellationToken));
            return;
        }

        // Rows come oldest first, so the last purchase entry is the latest one.
        var lastPurchase = rows.LastOrDefault(row => row is
            { PurchaseId: not null, QuantityMilli: > 0, UnitCostUsdMicros: not null, UnitCostCupMicros: not null });
        // Max over a reference type gives null on an empty sequence (no purchase left).
        var lastPurchaseDate = rows.Where(row => row is { PurchaseId: not null, QuantityMilli: > 0 })
            .Max(row => (string?)row.Date);
        var lastUsd = lastPurchase?.UnitCostUsdMicros ?? cost.UsdMicros;
        var lastCup = lastPurchase?.UnitCostCupMicros ?? cost.CupMicros;

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO product_costs (
                product_id, avg_cost_usd_micros, avg_cost_cup_micros, last_cost_usd_micros, last_cost_cup_micros,
                last_purchase_date, recalculated_at)
            VALUES (@productId, @usd, @cup, @lastUsd, @lastCup, @lastPurchaseDate, @now)
            ON CONFLICT (product_id) DO UPDATE SET
                avg_cost_usd_micros = excluded.avg_cost_usd_micros,
                avg_cost_cup_micros = excluded.avg_cost_cup_micros,
                last_cost_usd_micros = excluded.last_cost_usd_micros,
                last_cost_cup_micros = excluded.last_cost_cup_micros,
                last_purchase_date = excluded.last_purchase_date,
                recalculated_at = excluded.recalculated_at
            """, new { productId, usd = cost.UsdMicros, cup = cost.CupMicros, lastUsd, lastCup, lastPurchaseDate, now },
            transaction, cancellationToken: cancellationToken));
    }

    /// <summary>The date stays a plain ISO string here: max() over text dates is correct and needs no handler.</summary>
    private sealed class CostMovementRow
    {
        public long QuantityMilli { get; init; }
        public long? UnitCostUsdMicros { get; init; }
        public long? UnitCostCupMicros { get; init; }
        public string Date { get; init; } = "";
        public string? PurchaseId { get; init; }
    }
}
