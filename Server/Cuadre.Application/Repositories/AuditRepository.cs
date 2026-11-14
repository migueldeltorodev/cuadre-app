using System.Text.Json;
using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IAuditRepository
{
    Task RecordAsync(ConfigChange change, CancellationToken cancellationToken = default);

    /// <summary>Every entry with its moment in [fromUtc, toUtc), of the kinds asked; not sorted.</summary>
    Task<IReadOnlyList<AuditEvent>> GetEventsAsync(DateTime fromUtc, DateTime toUtc,
        IReadOnlySet<AuditEventKind> kinds, CancellationToken cancellationToken = default);
}

/// <summary>
/// D-59: the log is read where things are already kept, never edited (voided documents, voided closings, price changes,
/// plans, pay schemes) plus config_changes for what used to be overwritten. Small volumes: one query per source.
/// </summary>
public class AuditRepository : IAuditRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public AuditRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task RecordAsync(ConfigChange change, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO config_changes (id, changed_at, subject, subject_id, subject_name, changes)
            VALUES (@Id, @ChangedAt, @Subject, @SubjectId, @SubjectName, @Changes)
            """, new
        {
            change.Id,
            change.ChangedAt,
            Subject = change.Subject.ToString(),
            change.SubjectId,
            change.SubjectName,
            Changes = JsonSerializer.Serialize(change.Changes, Json)
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditEvent>> GetEventsAsync(DateTime fromUtc, DateTime toUtc,
        IReadOnlySet<AuditEventKind> kinds, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var range = new { fromUtc, toUtc };
        var events = new List<AuditEvent>();

        if (kinds.Contains(AuditEventKind.Voided))
        {
            events.AddRange(await VoidedDocumentsAsync(connection, range, cancellationToken));
        }

        if (kinds.Contains(AuditEventKind.DayReopened))
        {
            var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition("""
                SELECT id, date AS label, voided_at AS at, void_reason AS reason
                FROM daily_closings
                WHERE status = 'Voided' AND voided_at >= @fromUtc AND voided_at < @toUtc
                """, range, cancellationToken: cancellationToken));
            events.AddRange(rows.Select(row => new AuditEvent(row.At, AuditEventKind.DayReopened,
                AuditSubject.DailyClosing, row.Id, $"Cierre del {row.Label}", row.Reason, [])));
        }

        if (kinds.Contains(AuditEventKind.PriceChanged))
        {
            // A product's first price is where it starts, not a change.
            var rows = await connection.QueryAsync<PriceRow>(new CommandDefinition("""
                SELECT p.id AS product_id, p.code, p.name, c.created_at AS at, c.reason,
                       c.previous_price_cents, c.price_cents, c.effective_date
                FROM product_price_changes c
                JOIN products p ON p.id = c.product_id
                WHERE c.previous_price_cents IS NOT NULL AND c.created_at >= @fromUtc AND c.created_at < @toUtc
                """, range, cancellationToken: cancellationToken));
            events.AddRange(rows.Select(row => new AuditEvent(row.At, AuditEventKind.PriceChanged,
                AuditSubject.Product, row.ProductId, $"{row.Name} ({row.Code})",
                row.Reason == "Purchase" ? "Al confirmar una compra" : null,
                [
                    new FieldChange("Precio de venta", AuditFormat.Money(row.PreviousPriceCents!.Value),
                        AuditFormat.Money(row.PriceCents)),
                    new FieldChange("Rige desde", null, row.EffectiveDate)
                ])));
        }

        if (kinds.Contains(AuditEventKind.ProfitFirstPlan))
        {
            events.AddRange(await PlansAsync(connection, fromUtc, toUtc, cancellationToken));
        }

        if (kinds.Contains(AuditEventKind.PayScheme))
        {
            events.AddRange(await PaySchemesAsync(connection, fromUtc, toUtc, cancellationToken));
        }

        if (kinds.Contains(AuditEventKind.ConfigChanged))
        {
            var rows = await connection.QueryAsync<ConfigRow>(new CommandDefinition("""
                SELECT changed_at, subject, subject_id, subject_name, changes
                FROM config_changes
                WHERE changed_at >= @fromUtc AND changed_at < @toUtc
                """, range, cancellationToken: cancellationToken));
            events.AddRange(rows.Select(row =>
            {
                var subject = Enum.Parse<ConfigSubject>(row.Subject);
                return new AuditEvent(row.ChangedAt, AuditEventKind.ConfigChanged, ToAuditSubject(subject),
                    row.SubjectId, ConfigTitle(subject, row.SubjectName), null,
                    JsonSerializer.Deserialize<List<FieldChange>>(row.Changes, Json) ?? []);
            }));
        }

        return events;
    }

    // ---------- Voided documents ----------

    private static async Task<IEnumerable<AuditEvent>> VoidedDocumentsAsync(System.Data.IDbConnection connection,
        object range, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition("""
            SELECT 'Purchase' AS source, id, 'Compra #' || number AS label, voided_at AS at, void_reason AS reason
            FROM purchases WHERE status = 'Voided' AND voided_at >= @fromUtc AND voided_at < @toUtc
            UNION ALL
            SELECT 'StockOperation', id,
                   CASE kind WHEN 'Transfer' THEN 'Traslado #' WHEN 'Exit' THEN 'Salida #' WHEN 'Count' THEN 'Conteo #'
                             ELSE 'Operación #' END || number,
                   voided_at, void_reason
            FROM stock_operations WHERE status = 'Voided' AND voided_at >= @fromUtc AND voided_at < @toUtc
            UNION ALL
            SELECT 'EnvelopeOperation', id,
                   CASE kind WHEN 'Opening' THEN 'Saldo inicial #' WHEN 'Transfer' THEN 'Movimiento entre sobres #'
                             WHEN 'Withdrawal' THEN 'Retiro #' WHEN 'TaxPayment' THEN 'Pago de impuestos #'
                             ELSE 'Operación de sobres #' END || number,
                   voided_at, void_reason
            FROM envelope_operations WHERE status = 'Voided' AND voided_at >= @fromUtc AND voided_at < @toUtc
            UNION ALL
            SELECT 'Expense', id, 'Gasto #' || number, voided_at, void_reason
            FROM expenses WHERE status = 'Voided' AND voided_at >= @fromUtc AND voided_at < @toUtc
            UNION ALL
            SELECT 'ShortageRecovery', d.id, 'Recuperación del faltante del ' || d.date, r.voided_at, r.void_reason
            FROM cash_discrepancy_recoveries r JOIN cash_discrepancies d ON d.id = r.discrepancy_id
            WHERE r.status = 'Voided' AND r.voided_at >= @fromUtc AND r.voided_at < @toUtc
            """, range, cancellationToken: cancellationToken));

        return rows.Select(row => new AuditEvent(row.At, AuditEventKind.Voided, Enum.Parse<AuditSubject>(row.Source!),
            row.Id, row.Label, row.Reason, []));
    }

    // ---------- Plans and pay schemes: each one compared with the one before ----------

    private static async Task<IEnumerable<AuditEvent>> PlansAsync(System.Data.IDbConnection connection,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var items = (await connection.QueryAsync<PlanItemRow>(new CommandDefinition("""
            SELECT p.id AS plan_id, p.effective_from, p.notes, p.created_at, e.name AS envelope_name, e.sort_order,
                   i.percent_bps
            FROM allocation_plans p
            JOIN allocation_plan_items i ON i.plan_id = p.id
            JOIN envelopes e ON e.id = i.envelope_id
            ORDER BY p.effective_from, e.sort_order
            """, cancellationToken: cancellationToken))).ToList();

        var plans = items.GroupBy(item => item.PlanId).ToList();
        var events = new List<AuditEvent>();

        // The first plan is where the business started, not a change.
        for (var index = 1; index < plans.Count; index++)
        {
            var plan = plans[index].ToList();
            var head = plan[0];
            if (head.CreatedAt < fromUtc || head.CreatedAt >= toUtc)
            {
                continue;
            }

            var before = plans[index - 1].ToDictionary(item => item.EnvelopeName, item => item.PercentBps);
            var changes = new ChangeList();
            foreach (var item in plan)
            {
                changes.Text(item.EnvelopeName,
                    before.TryGetValue(item.EnvelopeName, out var bps) ? AuditFormat.Percent(bps) : null,
                    AuditFormat.Percent(item.PercentBps));
            }

            events.Add(new AuditEvent(head.CreatedAt, AuditEventKind.ProfitFirstPlan, AuditSubject.AllocationPlan,
                head.PlanId, $"Plan Profit First desde el {head.EffectiveFrom}", head.Notes, changes.Items));
        }

        return events;
    }

    private static async Task<IEnumerable<AuditEvent>> PaySchemesAsync(System.Data.IDbConnection connection,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var schemes = (await connection.QueryAsync<SchemeRow>(new CommandDefinition("""
            SELECT id, effective_from, daily_fixed_cents, percent_of_sales_bps, notes, created_at
            FROM staff_pay_schemes
            ORDER BY effective_from
            """, cancellationToken: cancellationToken))).ToList();

        var events = new List<AuditEvent>();
        for (var index = 1; index < schemes.Count; index++)
        {
            var scheme = schemes[index];
            if (scheme.CreatedAt < fromUtc || scheme.CreatedAt >= toUtc)
            {
                continue;
            }

            var before = schemes[index - 1];
            var changes = new ChangeList()
                .Money("Fijo por día", before.DailyFixedCents, scheme.DailyFixedCents)
                .Percent("Porcentaje de las ventas", before.PercentOfSalesBps, scheme.PercentOfSalesBps);
            events.Add(new AuditEvent(scheme.CreatedAt, AuditEventKind.PayScheme, AuditSubject.StaffPayScheme,
                scheme.Id, $"Pago del personal desde el {scheme.EffectiveFrom}", scheme.Notes, changes.Items));
        }

        return events;
    }

    // ---------- Config changes ----------

    private static AuditSubject ToAuditSubject(ConfigSubject subject) => subject switch
    {
        ConfigSubject.Settings => AuditSubject.Settings,
        ConfigSubject.CashBox => AuditSubject.CashBox,
        ConfigSubject.PaymentMethod => AuditSubject.PaymentMethod,
        ConfigSubject.ExchangeRate => AuditSubject.ExchangeRate,
        ConfigSubject.Envelope => AuditSubject.Envelope,
        ConfigSubject.Employee => AuditSubject.Employee,
        ConfigSubject.ExpenseCategory => AuditSubject.ExpenseCategory,
        _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, null)
    };

    private static string ConfigTitle(ConfigSubject subject, string name) => subject switch
    {
        ConfigSubject.Settings => "Datos del negocio y precios",
        ConfigSubject.CashBox => $"Caja «{name}»",
        ConfigSubject.PaymentMethod => $"Forma de cobro «{name}»",
        ConfigSubject.ExchangeRate => $"Tasa del dólar del {name}",
        ConfigSubject.Envelope => $"Sobre «{name}»",
        ConfigSubject.Employee => $"Empleado «{name}»",
        ConfigSubject.ExpenseCategory => $"Categoría de gasto «{name}»",
        _ => name
    };

    // ---------- Rows ----------

    private sealed class DocumentRow
    {
        public string? Source { get; set; }
        public Guid Id { get; set; }
        public string Label { get; set; } = "";
        public DateTime At { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class PriceRow
    {
        public Guid ProductId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime At { get; set; }
        public string Reason { get; set; } = "";
        public long? PreviousPriceCents { get; set; }
        public long PriceCents { get; set; }
        public string EffectiveDate { get; set; } = "";
    }

    private sealed class PlanItemRow
    {
        public Guid PlanId { get; set; }
        public string EffectiveFrom { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string EnvelopeName { get; set; } = "";
        public int SortOrder { get; set; }
        public int PercentBps { get; set; }
    }

    private sealed class SchemeRow
    {
        public Guid Id { get; set; }
        public string EffectiveFrom { get; set; } = "";
        public long DailyFixedCents { get; set; }
        public int PercentOfSalesBps { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class ConfigRow
    {
        public DateTime ChangedAt { get; set; }
        public string Subject { get; set; } = "";
        public Guid? SubjectId { get; set; }
        public string SubjectName { get; set; } = "";
        public string Changes { get; set; } = "";
    }
}
