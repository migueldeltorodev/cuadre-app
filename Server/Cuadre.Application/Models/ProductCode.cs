using System.Globalization;

namespace Cuadre.Application.Models;

/// <summary>Internal product codes "PREFIX-NNN" (ARR-001), generated per category and editable (RF-PRD-02).</summary>
public static class ProductCode
{
    /// <summary>
    /// Next free code after the highest numeric suffix in use: ARR-001, ARR-007 → ARR-008.
    /// Codes edited by hand that do not end in a number ("ARR-ESP") are ignored. Gaps are not reused,
    /// so a code once printed on a shelf label never points to a different product.
    /// </summary>
    public static string Next(string prefix, IEnumerable<string> existingCodes)
    {
        var start = prefix.Length + 1;
        var highest = existingCodes
            .Where(code => code.Length > start && code.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
            .Select(code => int.TryParse(code.AsSpan(start), NumberStyles.None, CultureInfo.InvariantCulture,
                out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix.ToUpperInvariant()}-{highest + 1:000}";
    }
}
