using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

public class TaxResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Basis points: 1000 = 10 %.</summary>
    public required int RateBps { get; init; }

    public required TaxBaseCode Base { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class TaxesResponse
{
    /// <summary>Active first, then by name.</summary>
    public required IEnumerable<TaxResponse> Items { get; init; } = [];
}
