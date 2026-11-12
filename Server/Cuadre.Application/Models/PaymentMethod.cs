namespace Cuadre.Application.Models;

/// <summary>
/// A "forma de cobro" (Efectivo, Transfermóvil, Enzona). Every payment collected with it
/// lands in its cash box (RF-CAJ-02). Never deleted, only deactivated.
/// </summary>
public class PaymentMethod
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required Guid CashBoxId { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
