using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

/// <summary>D-62. A tax only suggests how much to set aside: Profit First splits the same with or without it.</summary>
public class CreateTaxRequest
{
    public required string Name { get; init; }

    /// <summary>Basis points: 1000 = 10 %.</summary>
    public required int RateBps { get; init; }

    public required TaxBaseCode Base { get; init; }
}

/// <summary>Never deleted, only deactivated.</summary>
public class UpdateTaxRequest
{
    public required string Name { get; init; }
    public required int RateBps { get; init; }
    public required TaxBaseCode Base { get; init; }
    public required bool IsActive { get; init; }
}
