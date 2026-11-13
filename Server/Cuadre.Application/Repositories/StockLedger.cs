using System.Data;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>
/// The only writer of stock_movements. Every document (purchase, transfer, exit, count) appends its rows through
/// here, INSIDE its own transaction, so the document and its movements commit or fail together.
/// </summary>
internal static class StockLedger
{
    public static async Task AppendAsync(IDbConnection connection, IDbTransaction transaction,
        IEnumerable<StockMovement> movements, CancellationToken cancellationToken)
    {
        foreach (var movement in movements)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO stock_movements (id, product_id, location_id, date, kind, quantity_milli,
                                             unit_cost_usd_micros, unit_cost_cup_micros, purchase_id,
                                             purchase_line_id, operation_id, closing_id, created_at)
                VALUES (@Id, @ProductId, @LocationId, @Date, @Kind, @QuantityMilli,
                        @UnitCostUsdMicros, @UnitCostCupMicros, @PurchaseId, @PurchaseLineId, @OperationId,
                        @ClosingId, @CreatedAt)
                """, new
            {
                movement.Id,
                movement.ProductId,
                movement.LocationId,
                movement.Date,
                Kind = movement.Kind.ToString(),
                movement.QuantityMilli,
                movement.UnitCostUsdMicros,
                movement.UnitCostCupMicros,
                movement.PurchaseId,
                movement.PurchaseLineId,
                movement.OperationId,
                movement.ClosingId,
                movement.CreatedAt
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    /// <summary>Replays the average cost of every product touched, in the same transaction.</summary>
    public static async Task RecalculateCostsAsync(IDbConnection connection, IDbTransaction transaction,
        IEnumerable<Guid> productIds, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var productId in productIds.Distinct())
        {
            await ProductCostLedger.RecalculateAsync(connection, transaction, productId, now, cancellationToken);
        }
    }
}
