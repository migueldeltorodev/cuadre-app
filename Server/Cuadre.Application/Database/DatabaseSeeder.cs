using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Database;

/// <summary>
/// Factory defaults (RF-CFG-03, RF-CAJ-01). Runs at EVERY startup after the migrations and is idempotent:
///  - the settings row is inserted only if missing (INSERT OR IGNORE on the fixed id 1);
///  - cash boxes and payment methods only on a fresh install (no cash box at all). They are never deleted,
///    only deactivated, so "no rows" really means "first run".
/// Kept out of the migrations so the integration tests can wipe every table and re-seed.
/// </summary>
public sealed class DatabaseSeeder
{
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

        transaction.Commit();
        return;

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
