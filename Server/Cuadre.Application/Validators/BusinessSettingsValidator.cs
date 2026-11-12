using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

/// <summary>PURE shape rules (no I/O), so it can be a Singleton. Messages are shown in the UI: Spanish.</summary>
public class BusinessSettingsValidator : AbstractValidator<BusinessSettings>
{
    // Sanity ceilings: they catch typos (an extra zero), not business limits.
    private const long MaxCashThresholdCents = 1_000_000_00;   // 1,000,000 CUP
    private const long MaxExchangeBufferCents = 10_000_00;      // 10,000 CUP per USD
    private const int MaxMarkupBps = 100_000;                   // 1000 %
    private const long MaxRoundingStepCents = 1_000_00;         // 1,000 CUP

    public BusinessSettingsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del negocio es obligatorio.")
            .MaximumLength(120);

        RuleFor(x => x.Address)
            .MaximumLength(300);

        RuleFor(x => x.ClosedDays)
            .Must(days => days.Distinct().Count() < 7)
            .WithMessage("No se pueden marcar los siete días como días sin cierre.");

        RuleForEach(x => x.ClosedDays)
            .IsInEnum();

        RuleFor(x => x.CashDifferenceThresholdCents)
            .InclusiveBetween(0, MaxCashThresholdCents);

        RuleFor(x => x.ExchangeBufferCents)
            .InclusiveBetween(0, MaxExchangeBufferCents);

        RuleFor(x => x.DefaultMarkupBps)
            .InclusiveBetween(0, MaxMarkupBps);

        RuleFor(x => x.PriceRoundingStepCents)
            .InclusiveBetween(1, MaxRoundingStepCents)
            .WithMessage("El redondeo debe estar entre 0.01 y 1,000 CUP.");
    }
}
