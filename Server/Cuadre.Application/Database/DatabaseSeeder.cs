using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using Dapper;

namespace Cuadre.Application.Database;

/// <summary>
/// Factory defaults (RF-CFG-03, RF-CAJ-01). Runs at EVERY startup after the migrations and is idempotent:
///  - the settings row is inserted only if missing (INSERT OR IGNORE on the fixed id 1);
///  - cash boxes and payment methods only on a fresh install (no cash box at all). They are never deleted,
///    only deactivated, so "no rows" really means "first run";
///  - locations (Almacén, Mostrador) the same way, checked on their own so existing installs get them too;
///  - envelopes and the first allocation plan (slice 5) the same way. Then every cash movement still without an
///    envelope (the purchases recorded before slice 5) is assigned to Reposición, so the envelopes of each box
///    always add up to the box;
///  - the staff pay scheme (slice 6): 1,000 CUP per day + 1% of A1 (D-12), "since always", if there is none;
///  - the expense categories (slice 7) the same way as the cash boxes;
///  - the cost cache of products cached before D-60 (FIFO) is replayed once, to learn the batch being sold.
/// Kept out of the migrations so the integration tests can wipe every table and re-seed.
/// </summary>
public sealed class DatabaseSeeder
{
    /// <summary>The first allocation plan applies "since always" (the UI shows it as "desde el principio").</summary>
    public static readonly DateOnly InitialPlanDate = new(2000, 1, 1);

    /// <summary>The usual expenses of a small Cuban business; the owner renames, adds or deactivates them.</summary>
    public static readonly string[] DefaultExpenseCategories =
    [
        "Gestor y contador",
        "Transporte",
        "Servicios (luz, agua, teléfono)",
        "Alquiler",
        "Mantenimiento y reparaciones",
        "Envases y materiales",
        "Otros"
    ];

    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly TimeProvider _clock;

    public DatabaseSeeder(IDbConnectionFactory dbConnectionFactory, TimeProvider clock)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var now = _clock.UtcNow();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT OR IGNORE INTO business_settings
                (id, name, address, closed_days_mask, cash_difference_threshold_cents, exchange_buffer_cents,
                 default_markup_bps, price_rounding_step_cents, updated_at)
            VALUES (1, 'Mi negocio', NULL, 0, 5000, 0, 0, 100, @now)
            """, new { now }, transaction, cancellationToken: cancellationToken));

        var hasCashBoxes = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM cash_boxes)", transaction: transaction,
            cancellationToken: cancellationToken));

        if (!hasCashBoxes)
        {
            var cashCup = Guid.NewGuid();
            var bankCup = Guid.NewGuid();
            var cashUsd = Guid.NewGuid();

            await InsertCashBoxAsync(cashCup, "Efectivo CUP", Currency.CUP, CashBoxKind.Cash);
            await InsertCashBoxAsync(bankCup, "Banco CUP", Currency.CUP, CashBoxKind.Bank);
            await InsertCashBoxAsync(cashUsd, "Efectivo USD", Currency.USD, CashBoxKind.Cash);

            await InsertPaymentMethodAsync("Efectivo", cashCup);
            await InsertPaymentMethodAsync("Transfermóvil", bankCup);
            await InsertPaymentMethodAsync("Enzona", bankCup);
        }

        // Locations (RF-INV-01): never deleted either, so "none" means first run of slice 3+.
        var hasLocations = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM locations)", transaction: transaction,
            cancellationToken: cancellationToken));

        if (!hasLocations)
        {
            await InsertLocationAsync("Almacén", LocationKind.Storage);
            await InsertLocationAsync("Mostrador", LocationKind.Counter);
        }

        // Envelopes (RF-PF-01, RF-PF-04): never deleted either. Default % (P-01): the book's table for a small
        // real revenue. The plan starts "since always" so any past date has one.
        var hasEnvelopes = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM envelopes)", transaction: transaction,
            cancellationToken: cancellationToken));

        if (!hasEnvelopes)
        {
            var profit = await InsertEnvelopeAsync("Ganancia", EnvelopeRole.Profit, 1);
            var owner = await InsertEnvelopeAsync("Compensación del dueño", EnvelopeRole.OwnerPay, 2);
            var tax = await InsertEnvelopeAsync("Impuestos", EnvelopeRole.Tax, 3);
            var operations = await InsertEnvelopeAsync("Gastos de operación", EnvelopeRole.Operations, 4);
            await InsertEnvelopeAsync("Reposición de mercadería", EnvelopeRole.Replenishment, 5);

            var planId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO allocation_plans (id, effective_from, notes, created_at, updated_at)
                VALUES (@planId, @effectiveFrom, NULL, @now, @now)
                """, new { planId, effectiveFrom = InitialPlanDate, now }, transaction,
                cancellationToken: cancellationToken));

            foreach (var (envelopeId, percentBps) in new[] { (profit, 500), (owner, 5000), (tax, 1500), (operations, 3000) })
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO allocation_plan_items (plan_id, envelope_id, percent_bps)
                    VALUES (@planId, @envelopeId, @percentBps)
                    """, new { planId, envelopeId, percentBps }, transaction, cancellationToken: cancellationToken));
            }
        }

        // Staff pay (RN-08, D-12). Never deleted: "none" means first run of slice 6.
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO staff_pay_schemes (id, effective_from, daily_fixed_cents, percent_of_sales_bps, notes,
                                           created_at, updated_at)
            SELECT @id, @effectiveFrom, 100000, 100, NULL, @now, @now
            WHERE NOT EXISTS (SELECT 1 FROM staff_pay_schemes)
            """, new { id = Guid.NewGuid(), effectiveFrom = InitialPlanDate, now }, transaction,
            cancellationToken: cancellationToken));

        // Expense categories (RF-GAS-03): never deleted either, so "none" means first run of slice 7.
        var hasExpenseCategories = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM expense_categories)", transaction: transaction,
            cancellationToken: cancellationToken));

        if (!hasExpenseCategories)
        {
            foreach (var name in DefaultExpenseCategories)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO expense_categories (id, name, is_active, created_at, updated_at)
                    VALUES (@id, @name, 1, @now, @now)
                    """, new { id = Guid.NewGuid(), name, now }, transaction, cancellationToken: cancellationToken));
            }
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE cash_movements
            SET envelope_id = (SELECT id FROM envelopes WHERE role = 'Replenishment')
            WHERE envelope_id IS NULL
            """, transaction: transaction, cancellationToken: cancellationToken));

        // D-60/D-61: the cost cache learned the FIFO batch being sold and the newest purchase line. Products cached
        // before get them by a replay (one whose purchases were all voided has no line: it is replayed again, cheaply).
        var uncached = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT product_id FROM product_costs WHERE current_cost_usd_micros IS NULL OR last_purchase_line_id IS NULL",
            transaction: transaction, cancellationToken: cancellationToken));
        var replayed = uncached.ToList();
        await StockLedger.RecalculateCostsAsync(connection, transaction, replayed, now, cancellationToken);

        // Prices already in force were decided for the batch on sale: no "new batch" notice for them on this start.
        foreach (var productId in replayed)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE products
                SET price_reviewed_line_id =
                    (SELECT current_purchase_line_id FROM product_costs WHERE product_id = @productId)
                WHERE id = @productId AND price_reviewed_line_id IS NULL
                """, new { productId }, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
        return;

        async Task<Guid> InsertEnvelopeAsync(string name, EnvelopeRole role, int sortOrder)
        {
            var id = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO envelopes (id, name, role, sort_order, is_active, created_at, updated_at)
                VALUES (@id, @name, @role, @sortOrder, 1, @now, @now)
                """, new { id, name, role = role.ToString(), sortOrder, now },
                transaction, cancellationToken: cancellationToken));
            return id;
        }

        Task InsertLocationAsync(string name, LocationKind kind) =>
            connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO locations (id, name, kind, is_active, created_at, updated_at)
                VALUES (@id, @name, @kind, 1, @now, @now)
                """, new { id = Guid.NewGuid(), name, kind = kind.ToString(), now },
                transaction, cancellationToken: cancellationToken));

        Task InsertCashBoxAsync(Guid id, string name, Currency currency, CashBoxKind kind) =>
            connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO cash_boxes (id, name, currency, kind, is_active, created_at, updated_at)
                VALUES (@id, @name, @currency, @kind, 1, @now, @now)
                """, new { id, name, currency = currency.ToString(), kind = kind.ToString(), now },
                transaction, cancellationToken: cancellationToken));

        Task InsertPaymentMethodAsync(string name, Guid cashBoxId) =>
            connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO payment_methods (id, name, cash_box_id, is_active, created_at, updated_at)
                VALUES (@id, @name, @cashBoxId, 1, @now, @now)
                """, new { id = Guid.NewGuid(), name, cashBoxId, now },
                transaction, cancellationToken: cancellationToken));
    }
}
