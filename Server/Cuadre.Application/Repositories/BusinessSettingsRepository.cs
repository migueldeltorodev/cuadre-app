using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IBusinessSettingsRepository
{
    /// <summary>Null only if the database was never seeded (DatabaseSeeder runs at startup).</summary>
    Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(BusinessSettings settings, CancellationToken cancellationToken = default);
}

public class BusinessSettingsRepository : IBusinessSettingsRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public BusinessSettingsRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SettingsRow>(new CommandDefinition("""
            SELECT name, address, closed_days_mask, cash_difference_threshold_cents, exchange_buffer_cents,
                   default_markup_bps, price_rounding_step_cents, updated_at
            FROM business_settings
            WHERE id = 1
            """, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new BusinessSettings
            {
                Name = row.Name,
                Address = row.Address,
                ClosedDays = WeekdayMask.ToDays(row.ClosedDaysMask),
                CashDifferenceThresholdCents = row.CashDifferenceThresholdCents,
                ExchangeBufferCents = row.ExchangeBufferCents,
                DefaultMarkupBps = row.DefaultMarkupBps,
                PriceRoundingStepCents = row.PriceRoundingStepCents,
                UpdatedAt = row.UpdatedAt
            };
    }

    public async Task UpdateAsync(BusinessSettings settings, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE business_settings SET
                name = @Name,
                address = @Address,
                closed_days_mask = @ClosedDaysMask,
                cash_difference_threshold_cents = @CashDifferenceThresholdCents,
                exchange_buffer_cents = @ExchangeBufferCents,
                default_markup_bps = @DefaultMarkupBps,
                price_rounding_step_cents = @PriceRoundingStepCents,
                updated_at = @UpdatedAt
            WHERE id = 1
            """, new
        {
            settings.Name,
            settings.Address,
            ClosedDaysMask = WeekdayMask.From(settings.ClosedDays),
            settings.CashDifferenceThresholdCents,
            settings.ExchangeBufferCents,
            settings.DefaultMarkupBps,
            settings.PriceRoundingStepCents,
            settings.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    /// <summary>Flat shape of the table; the bit mask becomes a set of days in the model.</summary>
    private sealed class SettingsRow
    {
        public string Name { get; init; } = "";
        public string? Address { get; init; }
        public int ClosedDaysMask { get; init; }
        public long CashDifferenceThresholdCents { get; init; }
        public long ExchangeBufferCents { get; init; }
        public int DefaultMarkupBps { get; init; }
        public long PriceRoundingStepCents { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
