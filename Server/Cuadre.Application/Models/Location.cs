namespace Cuadre.Application.Models;

/// <summary>Where the goods are (RF-INV-01). Seeded: Almacén (Storage) and Mostrador (Counter).</summary>
public class Location
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required LocationKind Kind { get; init; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>What can change in a location: the kind is fixed once created.</summary>
public sealed record LocationChanges(Guid Id, string Name, bool IsActive);

/// <summary>
/// Storage = the warehouse (purchases land here by default).
/// Counter = the shop counter, the only place counted at the daily close (D-04).
/// </summary>
public enum LocationKind
{
    Storage,
    Counter
}
