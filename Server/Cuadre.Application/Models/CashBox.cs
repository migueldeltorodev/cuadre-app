namespace Cuadre.Application.Models;

/// <summary>
/// A "caja": WHERE the money physically is (Efectivo CUP, Banco CUP, Efectivo USD).
/// Not to be confused with a Profit First envelope, which says what the money is FOR (D-10).
/// Currency and Kind are immutable: changing them would corrupt every balance recorded in the box.
/// Never deleted, only deactivated.
/// </summary>
public class CashBox
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required Currency Currency { get; init; }
    public required CashBoxKind Kind { get; init; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>The only fields of a cash box that may change after creation.</summary>
public sealed record CashBoxChanges(Guid Id, string Name, bool IsActive);
