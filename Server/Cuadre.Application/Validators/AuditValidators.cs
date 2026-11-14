using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

public class GetAuditEventsOptionsValidator : AbstractValidator<GetAuditEventsOptions>
{
    public GetAuditEventsOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}
