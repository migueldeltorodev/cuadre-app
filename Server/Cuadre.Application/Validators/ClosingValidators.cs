using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Shape rules only. Which products are on the counter, which payment methods collect in CUP, whether the day is
// already closed... depend on the database, so the services check them (422).

public class EmployeeValidator : AbstractValidator<Employee>
{
    public EmployeeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(60);
    }
}

public class StaffPaySchemeValidator : AbstractValidator<StaffPayScheme>
{
    private const long MaxDailyFixedCents = 10_000_000_00L;   // catches typos only

    public StaffPaySchemeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DailyFixedCents)
            .GreaterThanOrEqualTo(0).WithMessage("El fijo diario no puede ser negativo.")
            .LessThanOrEqualTo(MaxDailyFixedCents).WithMessage("El fijo diario parece mal escrito.");
        RuleFor(x => x.PercentOfSalesBps)
            .InclusiveBetween(0, EnvelopeMath.FullPercentBps).WithMessage("El % de las ventas va de 0 a 100.");
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public class DailyClosingInputValidator : AbstractValidator<DailyClosingInput>
{
    private const long MaxAmountCents = 1_000_000_000_00L;   // catches typos only
    private const long MaxCountMilli = 1_000_000_000L;       // a million units
    private const int MaxLines = 2_000;

    public DailyClosingInputValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.EmployeeId).NotEqual(Guid.Empty).When(x => x.EmployeeId is not null);

        RuleFor(x => x.Counts)
            .Must(counts => counts.Count <= MaxLines).WithMessage($"Se admiten hasta {MaxLines} productos contados.");

        RuleForEach(x => x.Counts).ChildRules(count =>
        {
            count.RuleFor(x => x.ProductId).NotEmpty();
            count.RuleFor(x => x.LocationId).NotEmpty();
            count.RuleFor(x => x.CountedMilli)
                .GreaterThanOrEqualTo(0).WithMessage("Lo contado no puede ser negativo.")
                .LessThanOrEqualTo(MaxCountMilli).WithMessage("Lo contado parece mal escrito.");
        });

        RuleFor(x => x.Counts)
            .Must(counts => counts.Select(c => (c.ProductId, c.LocationId)).Distinct().Count() == counts.Count)
            .WithMessage("Un producto aparece contado dos veces en el mismo mostrador.");

        RuleForEach(x => x.Collections).ChildRules(collection =>
        {
            collection.RuleFor(x => x.PaymentMethodId).NotEmpty();
            collection.RuleFor(x => x.AmountCents)
                .GreaterThanOrEqualTo(0).WithMessage("Lo cobrado no puede ser negativo.")
                .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.");
        });

        RuleFor(x => x.Collections)
            .Must(collections => collections.Select(c => c.PaymentMethodId).Distinct().Count() == collections.Count)
            .WithMessage("Una forma de cobro aparece dos veces.");
    }
}

public class ReopenDailyClosingValidator : AbstractValidator<ReopenDailyClosing>
{
    public ReopenDailyClosingValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribí por qué se reabre.")
            .MaximumLength(300);
    }
}

public class GetAllDailyClosingsOptionsValidator : AbstractValidator<GetAllDailyClosingsOptions>
{
    public GetAllDailyClosingsOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}
