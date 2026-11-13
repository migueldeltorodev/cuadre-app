using System.Data;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>
/// The ONLY writer of cash_movements (like <see cref="StockLedger"/> for stock). Runs inside the caller's
/// transaction, so a document and its money movements are saved together or not at all.
/// </summary>
internal static class CashLedger
{
    public static async Task AppendAsync(IDbConnection connection, IDbTransaction transaction,
        IEnumerable<CashMovement> movements, CancellationToken cancellationToken)
    {
        foreach (var movement in movements)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO cash_movements (id, cash_box_id, envelope_id, date, kind, amount_cents, description,
                                            purchase_id, envelope_operation_id, stock_operation_id, closing_id,
                                            recovery_id, expense_id, created_at)
                VALUES (@Id, @CashBoxId, @EnvelopeId, @Date, @Kind, @AmountCents, @Description,
                        @PurchaseId, @EnvelopeOperationId, @StockOperationId, @ClosingId, @RecoveryId,
                        @ExpenseId, @CreatedAt)
                """, new
            {
                movement.Id,
                movement.CashBoxId,
                movement.EnvelopeId,
                movement.Date,
                Kind = movement.Kind.ToString(),
                movement.AmountCents,
                movement.Description,
                movement.PurchaseId,
                movement.EnvelopeOperationId,
                movement.StockOperationId,
                movement.ClosingId,
                movement.RecoveryId,
                movement.ExpenseId,
                movement.CreatedAt
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    /// <summary>
    /// "Is the document behind this row voided?" for any ledger query aliased m (cash_movements), p (purchases),
    /// eo (envelope_operations), so (stock_operations), dc (daily_closings: Voided = reopened) and
    /// rec (cash_discrepancy_recoveries), ex (expenses); rd is the shortage a recovery belongs to.
    /// </summary>
    public const string IsVoidedColumn =
        "COALESCE(p.status, eo.status, so.status, dc.status, rec.status, ex.status, 'Confirmed') = 'Voided' AS is_voided";

    public const string DocumentJoins = """
        LEFT JOIN purchases p ON p.id = m.purchase_id
        LEFT JOIN envelope_operations eo ON eo.id = m.envelope_operation_id
        LEFT JOIN stock_operations so ON so.id = m.stock_operation_id
        LEFT JOIN daily_closings dc ON dc.id = m.closing_id
        LEFT JOIN cash_discrepancy_recoveries rec ON rec.id = m.recovery_id
        LEFT JOIN cash_discrepancies rd ON rd.id = rec.discrepancy_id
        LEFT JOIN expenses ex ON ex.id = m.expense_id
        """;
}
