using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IExchangeRateRepository
{
    /// <summary>Insert, or overwrite the rate of that date (one rate per day). Keeps the original created_at.</summary>
    Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken = default);

    Task<ExchangeRate?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>The most recent rate on or before <paramref name="date"/>.</summary>
    Task<ExchangeRate?> GetLatestOnOrBeforeAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<IEnumerable<ExchangeRate>> GetAllAsync(GetAllExchangeRatesOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllExchangeRatesOptions options, CancellationToken cancellationToken = default);
}

public class ExchangeRateRepository : IExchangeRateRepository
{
    private const string SelectColumns = "date, cup_per_usd_cents, created_at, updated_at";

    // Shared by GetAll and GetCount so the total always matches the page.
    // ISO dates compare correctly as text.
    private const string RangeFilter = "(@from IS NULL OR date >= @from) AND (@to IS NULL OR date <= @to)";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public ExchangeRateRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO exchange_rates (date, cup_per_usd_cents, created_at, updated_at)
            VALUES (@Date, @CupPerUsdCents, @CreatedAt, @UpdatedAt)
            ON CONFLICT (date) DO UPDATE SET
                cup_per_usd_cents = excluded.cup_per_usd_cents,
                updated_at = excluded.updated_at
            """, rate, cancellationToken: cancellationToken));
    }

    public async Task<ExchangeRate?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ExchangeRate>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM exchange_rates WHERE date = @date",
            new { date }, cancellationToken: cancellationToken));
    }

    public async Task<ExchangeRate?> GetLatestOnOrBeforeAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ExchangeRate>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM exchange_rates WHERE date <= @date ORDER BY date DESC LIMIT 1",
            new { date }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<ExchangeRate>> GetAllAsync(GetAllExchangeRatesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<ExchangeRate>(new CommandDefinition($"""
            SELECT {SelectColumns}
            FROM exchange_rates
            WHERE {RangeFilter}
            ORDER BY date DESC
            LIMIT @pageSize OFFSET @offset
            """, new
        {
            from = options.From,
            to = options.To,
            pageSize = options.PageSize,
            offset = (options.Page - 1) * options.PageSize
        }, cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllExchangeRatesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM exchange_rates WHERE {RangeFilter}",
            new { from = options.From, to = options.To }, cancellationToken: cancellationToken));
    }
}
