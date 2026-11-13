namespace Cuadre.Application.Models;

/// <summary>
/// Someone who works the counter (RF-CIE-14). Only for the history and the claims of a shortage: the pay belongs to
/// the post (RN-08), not to the person. Never deleted, only deactivated.
/// </summary>
public class Employee
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record EmployeeChanges(Guid Id, string Name, bool IsActive);

/// <summary>
/// Pay of the post from a date on (RN-08, RF-PER-01): fixed per day + % of A1. A change is a new scheme, so the
/// closings already made keep what they paid (RF-PER-02).
/// </summary>
public class StaffPayScheme
{
    public required Guid Id { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public required long DailyFixedCents { get; init; }

    /// <summary>Basis points of A1 (what was really collected, D-19): 1% = 100.</summary>
    public required int PercentOfSalesBps { get; init; }

    public string? Notes { get; init; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
