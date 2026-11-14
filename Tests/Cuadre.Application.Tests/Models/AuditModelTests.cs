using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

public class ChangeListTests
{
    [Fact]
    public void ShouldKeepOnlyWhatReallyChanged()
    {
        var changes = new ChangeList()
            .Text("Nombre", "Efectivo CUP", "Efectivo CUP")
            .Text("Dirección", null, "  ")
            .Text("Nombre corto", " Caja ", "Caja")
            .Active(true, true)
            .Money("Colchón", 5_000, 5_000);

        changes.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ShouldWriteTheValuesForPeople()
    {
        var changes = new ChangeList()
            .Text("Nombre", "Efectivo", "Efectivo CUP")
            .Active(true, false)
            .Money("Colchón sobre la tasa", 5_000, 125_000)
            .Percent("Recargo por defecto", 2_000, 1_250);

        changes.Items.Should().Equal(
            new FieldChange("Nombre", "Efectivo", "Efectivo CUP"),
            new FieldChange("Estado", "Activo", "Inactivo"),
            new FieldChange("Colchón sobre la tasa", "50.00 CUP", "1,250.00 CUP"),
            new FieldChange("Recargo por defecto", "20 %", "12.5 %"));
    }

    [Fact]
    public void ShouldTreatBlankAsEmpty()
    {
        new ChangeList().Text("Dirección", "Calle 23", "").Items
            .Should().Equal(new FieldChange("Dirección", "Calle 23", null));
    }

    [Fact]
    public void Days_ShouldListTheWeekdaysInOrder_OrSayNone()
    {
        AuditFormat.Days([DayOfWeek.Saturday, DayOfWeek.Sunday]).Should().Be("domingo, sábado");
        AuditFormat.Days([]).Should().Be("ninguno");
    }
}
