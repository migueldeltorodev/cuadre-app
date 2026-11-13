using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Shape rules only. Existence, active flags, roles and currencies of envelopes and cash boxes depend on the
// database, so the services check them (422).

public class EnvelopeValidator : AbstractValidator<Envelope>
{
    public EnvelopeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del sobre es obligatorio.")
            .MaximumLength(40);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class AllocationPlanValidator : AbstractValidator<AllocationPlan>
{
    public AllocationPlanValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(300);

        RuleFor(x => x.Items).NotEmpty().WithMessage("Poné el % de al menos un sobre.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.EnvelopeId).NotEmpty();
            item.RuleFor(x => x.PercentBps)
                .InclusiveBetween(0, EnvelopeMath.FullPercentBps).WithMessage("Cada % va de 0 a 100.");
        });

        RuleFor(x => x.Items)
            .Must(items => items.Select(item => item.EnvelopeId).Distinct().Count() == items.Count)
            .WithMessage("Un sobre aparece dos veces.")
            .Must(items => items.Sum(item => item.PercentBps) == EnvelopeMath.FullPercentBps)
            .WithMessage("Los % tienen que sumar 100.")
            .When(x => x.Items.Count > 0);
    }
}

public class EnvelopeOperationValidator : AbstractValidator<EnvelopeOperation>
{
    private const long MaxAmountCents = 1_000_000_000_00L;   // catches typos only
    private const int MaxTargets = 200;

    public EnvelopeOperationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(500);

        // ---- Opening ----
        When(x => x.Kind == EnvelopeOperationKind.Opening, () =>
        {
            RuleFor(x => x.Targets)
                .NotEmpty().WithMessage("Escribí cuánto hay en al menos un sobre.")
                .Must(targets => targets.Count <= MaxTargets).WithMessage($"Se admiten hasta {MaxTargets} saldos.");

            RuleForEach(x => x.Targets).ChildRules(target =>
            {
                target.RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí la caja.");
                target.RuleFor(x => x.EnvelopeId).NotEmpty().WithMessage("Elegí el sobre.");
                target.RuleFor(x => x.TargetCents)
                    .GreaterThanOrEqualTo(0).WithMessage("Lo que hay en un sobre no puede ser negativo.")
                    .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.");
            });

            RuleFor(x => x.Targets)
                .Must(targets => targets.Select(t => (t.CashBoxId, t.EnvelopeId)).Distinct().Count() == targets.Count)
                .WithMessage("Un sobre aparece dos veces en la misma caja.");

            RuleFor(x => x.CashBoxId).Null();
            RuleFor(x => x.AmountCents).Null();
        });

        // ---- Single-box kinds ----
        When(x => x.Kind != EnvelopeOperationKind.Opening, () =>
        {
            RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí la caja.");
            RuleFor(x => x.AmountCents)
                .NotNull().WithMessage("Escribí el monto.")
                .GreaterThan(0).WithMessage("El monto tiene que ser mayor que cero.")
                .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.");
            RuleFor(x => x.Targets).Empty();
        });

        RuleFor(x => x.EnvelopeId)
            .NotEmpty().WithMessage("Elegí el sobre.")
            .When(x => x.Kind is EnvelopeOperationKind.Transfer or EnvelopeOperationKind.Withdrawal);

        RuleFor(x => x.ToEnvelopeId)
            .NotEmpty().WithMessage("Elegí a qué sobre va.")
            .When(x => x.Kind == EnvelopeOperationKind.Transfer);

        RuleFor(x => x.ToEnvelopeId)
            .NotEqual(x => x.EnvelopeId).WithMessage("El sobre de origen y el de destino tienen que ser distintos.")
            .When(x => x.Kind == EnvelopeOperationKind.Transfer && x.ToEnvelopeId is not null);

        RuleFor(x => x.ToEnvelopeId)
            .Null().WithMessage("Solo un movimiento entre sobres tiene destino.")
            .When(x => x.Kind != EnvelopeOperationKind.Transfer);

        RuleFor(x => x.Concept)
            .NotEmpty().WithMessage("Escribí qué impuesto se pagó.")
            .MaximumLength(120)
            .When(x => x.Kind == EnvelopeOperationKind.TaxPayment);

        RuleFor(x => x.Receipt).MaximumLength(60);
    }
}

public class VoidEnvelopeOperationValidator : AbstractValidator<VoidEnvelopeOperation>
{
    public VoidEnvelopeOperationValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribí por qué se anula.")
            .MaximumLength(300);
    }
}

public class GetAllEnvelopeOperationsOptionsValidator : AbstractValidator<GetAllEnvelopeOperationsOptions>
{
    public GetAllEnvelopeOperationsOptionsValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public class GetEnvelopeLedgerOptionsValidator : AbstractValidator<GetEnvelopeLedgerOptions>
{
    public GetEnvelopeLedgerOptionsValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}
