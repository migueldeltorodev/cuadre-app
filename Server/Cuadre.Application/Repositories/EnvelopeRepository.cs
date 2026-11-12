using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IEnvelopeRepository
{
    Task<bool> CreateAsync(Envelope envelope, CancellationToken cancellationToken = default);
    Task<Envelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Envelope?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>The one envelope with this role (never Custom). Seeded, so it always exists.</summary>
    Task<Envelope> GetByRoleAsync(EnvelopeRole role, CancellationToken cancellationToken = default);

    /// <summary>Active first, then by sort order.</summary>
    Task<IEnumerable<Envelope>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Envelope envelope, CancellationToken cancellationToken = default);
    Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default);

    /// <summary>True if the envelope holds money (positive or negative) in any cash box.</summary>
    Task<bool> HasBalanceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-zero balances per (envelope, box) at the end of <paramref name="asOf"/> (null = everything recorded).
    /// Filtered to one envelope when <paramref name="envelopeId"/> is given.
    /// </summary>
    Task<IReadOnlyList<EnvelopeBoxBalance>> GetBalancesAsync(DateOnly? asOf = null, Guid? envelopeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>The rows of one envelope in the range, oldest first, with names and document numbers.</summary>
    Task<IEnumerable<CashMovementEntry>> GetMovementsAsync(GetEnvelopeLedgerOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Every plan with its items, newest first.</summary>
    Task<IReadOnlyList<AllocationPlan>> GetPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>The plan in force on that date: the latest one starting on or before it.</summary>
    Task<AllocationPlan?> GetPlanForDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<AllocationPlan?> GetLatestPlanAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts the plan, or replaces the items of the plan with the same start date. One transaction.</summary>
    Task SavePlanAsync(AllocationPlan plan, bool replaceExisting, CancellationToken cancellationToken = default);
}

public class EnvelopeRepository : IEnvelopeRepository
{
    private const string SelectColumns = "id, name, role, sort_order, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public EnvelopeRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO envelopes (id, name, role, sort_order, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @Role, @SortOrder, @IsActive, @CreatedAt, @UpdatedAt)
            """, new
        {
            envelope.Id,
            envelope.Name,
            Role = envelope.Role.ToString(),
            envelope.SortOrder,
            envelope.IsActive,
            envelope.CreatedAt,
            envelope.UpdatedAt
        }, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Envelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Envelope>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM envelopes WHERE id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<Envelope?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Envelope>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM envelopes WHERE name = @name",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<Envelope> GetByRoleAsync(EnvelopeRole role, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Envelope>(new CommandDefinition(
                   $"SELECT {SelectColumns} FROM envelopes WHERE role = @role",
                   new { role = role.ToString() }, cancellationToken: cancellationToken))
               ?? throw new InvalidOperationException($"The {role} envelope is missing: the seeder did not run.");
    }

    public async Task<IEnumerable<Envelope>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Envelope>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM envelopes ORDER BY is_active DESC, sort_order, name",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        // role is deliberately NOT updatable.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE envelopes SET
                name = @Name,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { envelope.Id, envelope.Name, envelope.IsActive, envelope.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(sort_order), 0) + 1 FROM envelopes", cancellationToken: cancellationToken));
    }

    public async Task<bool> HasBalanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1 FROM cash_movements
                WHERE envelope_id = @id
                GROUP BY cash_box_id
                HAVING SUM(amount_cents) <> 0)
            """, new { id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EnvelopeBoxBalance>> GetBalancesAsync(DateOnly? asOf = null,
        Guid? envelopeId = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var balances = await connection.QueryAsync<EnvelopeBoxBalance>(new CommandDefinition("""
            SELECT envelope_id, cash_box_id, SUM(amount_cents) AS amount_cents
            FROM cash_movements
            WHERE envelope_id IS NOT NULL
              AND (@asOf IS NULL OR date <= @asOf)
              AND (@envelopeId IS NULL OR envelope_id = @envelopeId)
            GROUP BY envelope_id, cash_box_id
            HAVING SUM(amount_cents) <> 0
            """, new { asOf, envelopeId }, cancellationToken: cancellationToken));
        return balances.ToList();
    }

    public async Task<IEnumerable<CashMovementEntry>> GetMovementsAsync(GetEnvelopeLedgerOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CashMovementEntry>(new CommandDefinition($"""
            SELECT m.id, m.cash_box_id, b.name AS cash_box_name, b.currency, m.envelope_id, e.name AS envelope_name,
                   m.date, m.kind, m.amount_cents, m.description,
                   m.purchase_id, p.number AS purchase_number,
                   m.envelope_operation_id, eo.number AS envelope_operation_number,
                   m.stock_operation_id, so.number AS stock_operation_number,
                   {CashLedger.IsVoidedColumn}, m.created_at
            FROM cash_movements m
                JOIN cash_boxes b ON b.id = m.cash_box_id
                JOIN envelopes e ON e.id = m.envelope_id
                {CashLedger.DocumentJoins}
            WHERE m.envelope_id = @envelopeId
              AND (@cashBoxId IS NULL OR m.cash_box_id = @cashBoxId)
              AND (@from IS NULL OR m.date >= @from)
              AND (@to IS NULL OR m.date <= @to)
            ORDER BY m.date, m.created_at, m.rowid
            """, new
        {
            envelopeId = options.EnvelopeId,
            cashBoxId = options.CashBoxId,
            from = options.From,
            to = options.To
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AllocationPlan>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var plans = (await connection.QueryAsync<AllocationPlan>(new CommandDefinition("""
            SELECT id, effective_from, notes, created_at, updated_at
            FROM allocation_plans
            ORDER BY effective_from DESC
            """, cancellationToken: cancellationToken))).ToList();

        var items = await connection.QueryAsync<AllocationPlanItem>(new CommandDefinition("""
            SELECT i.plan_id, i.envelope_id, i.percent_bps, e.name AS envelope_name
            FROM allocation_plan_items i
                JOIN envelopes e ON e.id = i.envelope_id
            ORDER BY e.sort_order
            """, cancellationToken: cancellationToken));

        var byPlan = items.ToLookup(item => item.PlanId);
        foreach (var plan in plans)
        {
            plan.Items.AddRange(byPlan[plan.Id]);
        }

        return plans;
    }

    public async Task<AllocationPlan?> GetPlanForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        // Few plans in a lifetime: reading them all keeps one query shape for every lookup.
        var plans = await GetPlansAsync(cancellationToken);
        return plans.FirstOrDefault(plan => plan.EffectiveFrom <= date);
    }

    public async Task<AllocationPlan?> GetLatestPlanAsync(CancellationToken cancellationToken = default)
    {
        var plans = await GetPlansAsync(cancellationToken);
        return plans.FirstOrDefault();
    }

    public async Task SavePlanAsync(AllocationPlan plan, bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        if (replaceExisting)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM allocation_plan_items WHERE plan_id = @Id",
                new { plan.Id }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE allocation_plans SET notes = @Notes, updated_at = @UpdatedAt WHERE id = @Id
                """, new { plan.Id, plan.Notes, plan.UpdatedAt }, transaction, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO allocation_plans (id, effective_from, notes, created_at, updated_at)
                VALUES (@Id, @EffectiveFrom, @Notes, @CreatedAt, @UpdatedAt)
                """, new { plan.Id, plan.EffectiveFrom, plan.Notes, plan.CreatedAt, plan.UpdatedAt },
                transaction, cancellationToken: cancellationToken));
        }

        foreach (var item in plan.Items)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO allocation_plan_items (plan_id, envelope_id, percent_bps)
                VALUES (@PlanId, @EnvelopeId, @PercentBps)
                """, new { PlanId = plan.Id, item.EnvelopeId, item.PercentBps },
                transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }
}
