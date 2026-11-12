using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

public class ProductCodeTests
{
    [Fact]
    public void Next_ShouldStartAtOne_WhenCategoryHasNoProducts()
    {
        ProductCode.Next("ARR", []).Should().Be("ARR-001");
    }

    [Fact]
    public void Next_ShouldFollowTheHighestNumber_NotFillGaps()
    {
        ProductCode.Next("ARR", ["ARR-001", "ARR-007", "ARR-003"]).Should().Be("ARR-008");
    }

    [Fact]
    public void Next_ShouldIgnoreHandEditedCodes_WithoutANumber()
    {
        ProductCode.Next("ARR", ["ARR-ESP", "ARR-002", "ARR-"]).Should().Be("ARR-003");
    }

    [Fact]
    public void Next_ShouldIgnoreOtherPrefixesThatStartTheSame()
    {
        // "ARRO-050" belongs to another category whose prefix happens to start with ARR.
        ProductCode.Next("ARR", ["ARR-002", "ARRO-050"]).Should().Be("ARR-003");
    }

    [Fact]
    public void Next_ShouldGrowPastThreeDigits()
    {
        ProductCode.Next("ARR", ["ARR-999"]).Should().Be("ARR-1000");
    }
}

public class CategoryPrefixTests
{
    [Theory]
    [InlineData("Arroz", "ARR")]
    [InlineData("Café", "CAF")]
    [InlineData("Aceite y grasas", "ACE")]
    [InlineData("Ñame", "NAM")]
    [InlineData("Té", "TE")]
    public void DerivePrefix_ShouldTakeTheFirstThreeLetters_WithoutAccents(string name, string expected)
    {
        Category.DerivePrefix(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("A")]
    [InlineData("¿?")]
    public void DerivePrefix_ShouldReturnNull_WhenThereAreFewerThanTwoLetters(string name)
    {
        Category.DerivePrefix(name).Should().BeNull();
    }
}

public class MeasureUnitTests
{
    [Theory]
    [InlineData(MeasureUnit.Pound, true)]
    [InlineData(MeasureUnit.Kilogram, true)]
    [InlineData(MeasureUnit.Liter, true)]
    [InlineData(MeasureUnit.Each, false)]
    [InlineData(MeasureUnit.Sack, false)]
    [InlineData(MeasureUnit.Box, false)]
    public void AllowsDecimals_ShouldBeTrueOnlyForBulkUnits(MeasureUnit unit, bool expected)
    {
        unit.AllowsDecimals().Should().Be(expected);
    }
}
