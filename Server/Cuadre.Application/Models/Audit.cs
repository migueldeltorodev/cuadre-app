using System.Globalization;

namespace Cuadre.Application.Models;

// RF-DAT-08: the audit log. What was voided, which days were reopened, which prices and which settings changed,
// when and why. Everything but the settings changes is read from tables that already keep it (D-59).

/// <summary>What changed in Configuración (the config_changes rows).</summary>
public enum ConfigSubject
{
    Settings,
    CashBox,
    PaymentMethod,
    ExchangeRate,
    Envelope,
    Employee,
    ExpenseCategory,
    Tax
}

/// <summary>One field: before and after, already written for people ("20 %", "Sí").</summary>
public sealed record FieldChange(string Field, string? Before, string? After);

/// <summary>
/// Collects only what really changed: saving a form without touching it leaves nothing to log.
/// Values are formatted here, when they change: the history must read the same in five years.
/// </summary>
public sealed class ChangeList
{
    private readonly List<FieldChange> _items = [];

    public IReadOnlyList<FieldChange> Items => _items;
    public bool IsEmpty => _items.Count == 0;

    public ChangeList Text(string field, string? before, string? after)
    {
        var from = string.IsNullOrWhiteSpace(before) ? null : before.Trim();
        var to = string.IsNullOrWhiteSpace(after) ? null : after.Trim();
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            _items.Add(new FieldChange(field, from, to));
        }

        return this;
    }

    public ChangeList Active(bool before, bool after) =>
        Text("Estado", before ? "Activo" : "Inactivo", after ? "Activo" : "Inactivo");

    public ChangeList Money(string field, long beforeCents, long afterCents) =>
        Text(field, AuditFormat.Money(beforeCents), AuditFormat.Money(afterCents));

    public ChangeList Percent(string field, int beforeBps, int afterBps) =>
        Text(field, AuditFormat.Percent(beforeBps), AuditFormat.Percent(afterBps));
}

public static class AuditFormat
{
    private static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;

    /// <summary>125000 → "1,250.00 CUP".</summary>
    public static string Money(long cents, Currency currency = Currency.CUP) =>
        $"{(cents / 100m).ToString("N2", Numbers)} {currency}";

    /// <summary>2000 → "20 %", 1250 → "12.5 %".</summary>
    public static string Percent(int bps) => $"{(bps / 100m).ToString("0.##", Numbers)} %";

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", Numbers);

    private static readonly string[] Weekdays = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    /// <summary>[Sunday, Saturday] → "domingo, sábado"; none → "ninguno".</summary>
    public static string Days(IEnumerable<DayOfWeek> days)
    {
        var list = days.Distinct().Order().Select(day => Weekdays[(int)day]).ToList();
        return list.Count == 0 ? "ninguno" : string.Join(", ", list);
    }
}

/// <summary>A config_changes row.</summary>
public sealed record ConfigChange(
    Guid Id,
    DateTime ChangedAt,
    ConfigSubject Subject,
    Guid? SubjectId,
    string SubjectName,
    IReadOnlyList<FieldChange> Changes);

/// <summary>The kinds of entries in the log.</summary>
public enum AuditEventKind
{
    /// <summary>A purchase, stock operation, envelope operation, expense or shortage recovery voided.</summary>
    Voided,

    /// <summary>A closed day reopened (its closing voided).</summary>
    DayReopened,

    /// <summary>A product's sale price changed (RF-PRD-05).</summary>
    PriceChanged,

    /// <summary>A new Profit First plan (the % of each envelope from a date on).</summary>
    ProfitFirstPlan,

    /// <summary>A new pay for the counter from a date on.</summary>
    PayScheme,

    /// <summary>Something changed in Configuración.</summary>
    ConfigChanged
}

/// <summary>What the entry is about: the screen it links to.</summary>
public enum AuditSubject
{
    Purchase,
    StockOperation,
    EnvelopeOperation,
    Expense,
    ShortageRecovery,
    DailyClosing,
    Product,
    AllocationPlan,
    StaffPayScheme,
    Settings,
    CashBox,
    PaymentMethod,
    ExchangeRate,
    Envelope,
    Employee,
    ExpenseCategory,
    Tax
}

/// <summary>One line of the log. Title is ready to show ("Compra #12", "Cierre del 2026-11-10").</summary>
public sealed record AuditEvent(
    DateTime At,
    AuditEventKind Kind,
    AuditSubject Subject,
    Guid? SubjectId,
    string Title,
    string? Reason,
    IReadOnlyList<FieldChange> Changes);

/// <summary>From/To are the shop's calendar days (local), both included.</summary>
public class GetAuditEventsOptions
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public AuditEventKind? Kind { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record AuditEventsPage(IReadOnlyList<AuditEvent> Items, int Total);
