namespace Cuadre.Application.Models;

/// <summary>Supplier (RF-PRV-01). Never deleted: purchases will reference it. Deactivated instead.</summary>
public class Supplier
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
