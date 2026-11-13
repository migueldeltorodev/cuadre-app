namespace Cuadre.Contracts.Responses;

public class EmployeeResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class EmployeesResponse
{
    /// <summary>Active first, then by name.</summary>
    public required IEnumerable<EmployeeResponse> Items { get; init; } = [];
}

public class StaffPaySchemeResponse
{
    public required Guid Id { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public required long DailyFixedCents { get; init; }
    public required int PercentOfSalesBps { get; init; }
    public string? Notes { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class StaffPaySchemesResponse
{
    /// <summary>Newest first: the first one is the one in force from its date on.</summary>
    public required IEnumerable<StaffPaySchemeResponse> Items { get; init; } = [];
}
