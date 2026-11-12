using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

public class CashBoxValidator : AbstractValidator<CashBox>
{
    public CashBoxValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la caja es obligatorio.")
            .MaximumLength(60);

        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Kind).IsInEnum();
    }
}
