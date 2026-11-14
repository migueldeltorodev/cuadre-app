using System.Data;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>
/// Builds the FIFO batches (D-60) of some products by replaying their stock ledger. Movements of VOIDED purchases,
/// stock operations and reopened closings are left out entirely (the original rows and their reversals), exactly
/// like the average replay: the batches are what they would be if that document never happened.
/// </summary>
internal static class FifoLedger
{
    public static async Task<Dictionary<Guid, FifoBook>> LoadAsync(IDbConnection connection,
        IDbTransaction? transaction, IReadOnlyCollection<Guid> productIds, DateOnly? upToDate,
        CancellationToken cancellationToken)
    {
        var books = productIds.Distinct().ToDictionary(id => id, _ => new FifoBook());
        if (books.Count == 0)
        {
            return books;
        }

        var rows = await connection.QueryAsync<FifoRow>(new CommandDefinition("""
            SELECT m.id, m.product_id, m.date, m.kind, m.quantity_milli, m.unit_cost_usd_micros,
                   m.unit_cost_cup_micros, m.purchase_line_id
            FROM stock_movements m
                LEFT JOIN purchases p ON p.id = m.purchase_id
                LEFT JOIN stock_operations o ON o.id = m.operation_id
                LEFT JOIN daily_closings c ON c.id = m.closing_id
            WHERE m.product_id IN @ids
              AND (@upTo IS NULL OR m.date <= @upTo)
              AND (p.id IS NULL OR p.status = 'Confirmed')
              AND (o.id IS NULL OR o.status = 'Confirmed')
              AND (c.id IS NULL OR c.status = 'Confirmed')
            ORDER BY m.date, m.created_at, m.rowid
            """,
            // Ids go as text: Dapper's IN-list expansion does not run our Guid handler on each element.
            new { ids = books.Keys.Select(id => id.ToString("D")).ToList(), upTo = upToDate }, transaction,
            cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            books[row.ProductId].Apply(new FifoMovement(row.Id, row.Date,
                Enum.Parse<StockMovementKind>(row.Kind), row.QuantityMilli, row.UnitCostUsdMicros,
                row.UnitCostCupMicros, row.PurchaseLineId));
        }

        return books;
    }

    private sealed class FifoRow
    {
        public Guid Id { get; init; }
        public Guid ProductId { get; init; }
        public DateOnly Date { get; init; }
        public string Kind { get; init; } = "";
        public long QuantityMilli { get; init; }
        public long? UnitCostUsdMicros { get; init; }
        public long? UnitCostCupMicros { get; init; }
        public Guid? PurchaseLineId { get; init; }
    }
}
