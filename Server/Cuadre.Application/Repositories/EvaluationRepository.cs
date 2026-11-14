using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>Read-only sums for the Profit First evaluation (RF-PF-10). Only confirmed closings count.</summary>
public interface IEvaluationRepository
{
    Task<EvaluationTotals> GetTotalsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Per envelope: its split in the closings plus the recoveries that came in during the period.</summary>
    Task<IReadOnlyList<EnvelopeReceived>> GetReceivedAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}

public class EvaluationRepository : IEvaluationRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public EvaluationRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<EvaluationTotals> GetTotalsAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<EvaluationTotals>(new CommandDefinition("""
            SELECT COUNT(*) AS closed_days,
                   COALESCE(SUM(c.cash_collected_cents), 0) AS cash_collected_cents,
                   COALESCE(SUM(c.transfer_collected_cents), 0) AS transfer_collected_cents,
                   COALESCE(SUM(c.cost_of_sales_cents), 0) AS cost_of_sales_cents,
                   COALESCE(SUM(c.theoretical_sales_cents), 0) AS theoretical_sales_cents,
                   COALESCE(SUM(c.staff_pay_cents), 0) AS staff_pay_cents,
                   COALESCE(SUM(CASE WHEN c.adjusted_difference_cents < 0
                                     THEN -c.adjusted_difference_cents ELSE 0 END), 0) AS shortage_cents,
                   COALESCE(SUM(CASE WHEN c.adjusted_difference_cents > 0
                                     THEN c.adjusted_difference_cents ELSE 0 END), 0) AS surplus_cents,
                   (SELECT COALESCE(SUM(r.amount_cents), 0) FROM cash_discrepancy_recoveries r
                    WHERE r.status = 'Confirmed' AND r.date BETWEEN @from AND @to) AS recovered_cents
            FROM daily_closings c
            WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to
            """, new { from, to }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EnvelopeReceived>> GetReceivedAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<EnvelopeReceived>(new CommandDefinition("""
            SELECT envelope_id, SUM(amount_cents) AS amount_cents
            FROM (
                SELECT a.envelope_id, a.amount_cents
                FROM daily_closing_allocations a
                    JOIN daily_closings c ON c.id = a.closing_id
                WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to
                UNION ALL
                SELECT m.envelope_id, m.amount_cents
                FROM cash_movements m
                    JOIN cash_discrepancy_recoveries r ON r.id = m.recovery_id
                WHERE m.kind = 'ShortageRecovery' AND r.status = 'Confirmed' AND m.date BETWEEN @from AND @to)
            GROUP BY envelope_id
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
