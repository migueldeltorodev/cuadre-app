using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

/// <summary>"Not in the future" depends on the clock, so it is a service rule (422), not a shape rule.</summary>
public class ExchangeRateValidator : AbstractValidator<ExchangeRate>
{
    private const long MaxCupPerUsdCents = 100_000_00;   // 100,000 CUP per USD: catches typos only

    public ExchangeRateValidator()
    {
        RuleFor(x => x.Date).NotEmpty();

        RuleFor(x => x.CupPerUsdCents)
            .GreaterThan(0).WithMessage("La tasa tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxCupPerUsdCents).WithMessage("La tasa parece mal escrita (demasiado alta).");
    }
}

public class GetAllExchangeRatesOptionsValidator : AbstractValidator<GetAllExchangeRatesOptions>
{
    public GetAllExchangeRatesOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha inicial no puede ser posterior a la final.");
    }
}
