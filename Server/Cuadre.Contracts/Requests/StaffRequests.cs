namespace Cuadre.Contracts.Requests;

public class CreateEmployeeRequest
{
    public required string Name { get; init; }
}

/// <summary>Never deleted, only deactivated: the closings keep who worked.</summary>
public class UpdateEmployeeRequest
{
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>
/// Pay of the post from a date on (RN-08): fixed per day + % of A1 in basis points (1% = 100). The same date as the
/// latest scheme replaces it; the closed days keep what they paid.
/// </summary>
public class CreateStaffPaySchemeRequest
{
    public required DateOnly EffectiveFrom { get; init; }
    public required long DailyFixedCents { get; init; }
    public required int PercentOfSalesBps { get; init; }
    public string? Notes { get; init; }
}
