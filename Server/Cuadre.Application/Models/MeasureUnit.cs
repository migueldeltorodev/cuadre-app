namespace Cuadre.Application.Models;

/// <summary>
/// Fixed list of units (decision 2026-11-12: in code, not a user table). Stored as TEXT.
/// "Each" = unidad. Adding one is a code change: append it, never reorder or rename (rows store the name).
/// </summary>
public enum MeasureUnit
{
    Each,
    Pound,
    Kilogram,
    Liter,
    Sack,
    Box,
    Pack,
    Dozen,
    Bag
}

public static class MeasureUnitRules
{
    /// <summary>
    /// Bulk units accept fractional quantities (2.5 lb); countable ones do not.
    /// Derived, never stored: a stored flag could contradict the unit ("unidad con decimales").
    /// </summary>
    public static bool AllowsDecimals(this MeasureUnit unit) =>
        unit is MeasureUnit.Pound or MeasureUnit.Kilogram or MeasureUnit.Liter;
}
