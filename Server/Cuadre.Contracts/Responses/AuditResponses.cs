using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

/// <summary>One field, before and after, already written for people ("20 %", "Activo").</summary>
public class FieldChangeResponse
{
    public required string Field { get; init; }
    public string? Before { get; init; }
    public string? After { get; init; }
}

public class AuditEventResponse
{
    public required DateTime At { get; init; }
    public required AuditEventKindCode Kind { get; init; }
    public required AuditSubjectCode Subject { get; init; }

    /// <summary>For the link to the document or item; null for the settings and the rates.</summary>
    public Guid? SubjectId { get; init; }

    /// <summary>Ready to show: "Compra #12", "Cierre del 2026-11-10", "Caja «Efectivo CUP»".</summary>
    public required string Title { get; init; }

    /// <summary>Why it was voided or reopened, or the notes of a plan.</summary>
    public string? Reason { get; init; }

    public required IReadOnlyList<FieldChangeResponse> Changes { get; init; } = [];
}
