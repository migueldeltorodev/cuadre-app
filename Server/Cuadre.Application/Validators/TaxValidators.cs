using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// D-62, shape rules only. The unique name depends on the database: the service checks it (409).

public class TaxValidator : AbstractValidator<Tax>
{
    public TaxValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Escribí el nombre del impuesto.")
            .MaximumLength(60);
        RuleFor(x => x.RateBps)
            .InclusiveBetween(1, 10_000).WithMessage("La tasa tiene que ser mayor que 0 % y hasta 100 %.");
        RuleFor(x => x.Base).IsInEnum().WithMessage("Elegí si se calcula sobre las ventas o sobre la ganancia.");
    }
}
