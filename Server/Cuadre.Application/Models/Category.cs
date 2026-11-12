using System.Globalization;
using System.Text;

namespace Cuadre.Application.Models;

/// <summary>
/// Two levels only (RF-PRD-04): a category (ParentId null, has a CodePrefix used for product codes)
/// or a subcategory (ParentId set, no prefix).
/// </summary>
public class Category
{
    public required Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public required string Name { get; set; }
    public string? CodePrefix { get; set; }

    /// <summary>Read-only usage info filled by queries (products in it or in its subcategories). Not persisted.</summary>
    public int ProductCount { get; init; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsTopLevel => ParentId is null;

    /// <summary>"Arroz" → "ARR", "Café" → "CAF", "Aceite y grasas" → "ACE". Null if fewer than 2 letters.</summary>
    public static string? DerivePrefix(string name)
    {
        var letters = name.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsAsciiLetter)
            .Take(3)
            .ToArray();

        return letters.Length >= 2 ? new string(letters).ToUpperInvariant() : null;
    }
}

/// <summary>What may change after creation. The level (top vs sub) never changes.</summary>
public sealed record CategoryChanges(Guid Id, string Name, string? CodePrefix);
