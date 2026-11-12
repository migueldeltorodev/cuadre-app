using System.Data;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>
/// Rebuilds the product_costs cache of one product by replaying its stock ledger (CostCalculator).
/// Runs INSIDE the caller's transaction, so a purchase/void and its new costs commit (or fail) together.
/// Movements of VOIDED purchases are left out entirely (both the entry and its reversal): the replay then
/// gives exactly the cost the product would have had if that purchase never happened.
/// </summary>
internal static class ProductCostLedger
{
    public static async Task RecalculateAsync(IDbConnection connection, IDbTransaction transaction,
        Guid productId, DateTime now, CancellationToken cancellationToken)
    {
        var movements = await connection.QueryAsync<CostMovementRow>(new CommandDefinition("""
            SELECT m.quantity_milli, m.unit_cost_usd_micros, m.unit_cost_cup_micros, m.date
            FROM stock_movements m
                LEFT JOIN purchases p ON p.id = m.purchase_id
            WHERE m.product_id = @productId
              AND (p.id IS NULL OR p.status = 'Confirmed')
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

        var lastPurchaseDate = rows.Where(row => row.UnitCostUsdMicros is not null).Max(row => row.Date);

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO product_costs (product_id, avg_cost_usd_micros, avg_cost_cup_micros, last_purchase_date, recalculated_at)
            VALUES (@productId, @usd, @cup, @lastPurchaseDate, @now)
            ON CONFLICT (product_id) DO UPDATE SET
                avg_cost_usd_micros = excluded.avg_cost_usd_micros,
                avg_cost_cup_micros = excluded.avg_cost_cup_micros,
                last_purchase_date = excluded.last_purchase_date,
                recalculated_at = excluded.recalculated_at
            """, new { productId, usd = cost.UsdMicros, cup = cost.CupMicros, lastPurchaseDate, now },
            transaction, cancellationToken: cancellationToken));
    }

    /// <summary>The date stays a plain ISO string here: max() over text dates is correct and needs no handler.</summary>
    private sealed class CostMovementRow
    {
        public long QuantityMilli { get; init; }
        public long? UnitCostUsdMicros { get; init; }
        public long? UnitCostCupMicros { get; init; }
        public string Date { get; init; } = "";
    }
}
