using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

/// <summary>Whether the cash box EXISTS and is active is a data rule: it lives in the service (422).</summary>
public class PaymentMethodValidator : AbstractValidator<PaymentMethod>
{
    public PaymentMethodValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la forma de cobro es obligatorio.")
            .MaximumLength(60);

        RuleFor(x => x.CashBoxId)
            .NotEmpty().WithMessage("Hay que elegir la caja a la que entra el cobro.");
    }
}
