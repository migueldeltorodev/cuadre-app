using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Slice 6b, shape rules only. Whether the shortage is still pending, how much is left of it, whether the day is open
// or the cash box collects in CUP depend on the database: the services check them (422).

public class RecoverShortageValidator : AbstractValidator<RecoverShortage>
{
    private const long MaxAmountCents = 1_000_000_000_00L;   // catches typos only

    public RecoverShortageValidator()
    {
        RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí en qué caja entra el dinero.");
        RuleFor(x => x.AmountCents)
            .GreaterThan(0).WithMessage("Lo recuperado tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.");
        RuleFor(x => x.Note).MaximumLength(300);
    }
}

public class WriteOffShortageValidator : AbstractValidator<WriteOffShortage>
{
    public WriteOffShortageValidator()
    {
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("Escribí por qué se da por perdido.")
            .MaximumLength(300);
    }
}

public class VoidShortageRecoveryValidator : AbstractValidator<VoidShortageRecovery>
{
    public VoidShortageRecoveryValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribí por qué se anula.")
            .MaximumLength(300);
    }
}

public class GetAllCashDiscrepanciesOptionsValidator : AbstractValidator<GetAllCashDiscrepanciesOptions>
{
    public GetAllCashDiscrepanciesOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.EmployeeId).NotEqual(Guid.Empty).When(x => x.EmployeeId is not null);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public class GetEvaluationOptionsValidator : AbstractValidator<GetEvaluationOptions>
{
    private const int MaxDays = 366 * 3;

    public GetEvaluationOptionsValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber <= MaxDays)
            .WithMessage("El período puede ser de hasta 3 años.")
            .When(x => x.To >= x.From);
    }
}
