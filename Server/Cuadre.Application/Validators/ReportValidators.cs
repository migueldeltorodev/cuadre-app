using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Slice 8, shape rules only. Reports read; they never refuse because of the data.

internal static class ReportRules
{
    /// <summary>Same limit as the evaluation (RF-PF-10): enough for any comparison, cheap for SQLite.</summary>
    public const int MaxDays = 366 * 3;

    public const string ToBeforeFrom = "La fecha 'hasta' no puede ser anterior a 'desde'.";
    public const string TooLong = "El período puede ser de hasta 3 años.";

    public static bool FitsInLimit(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber <= MaxDays;
}

public class ReportPeriodOptionsValidator : AbstractValidator<ReportPeriodOptions>
{
    public ReportPeriodOptionsValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage(ReportRules.ToBeforeFrom);
        RuleFor(x => x)
            .Must(x => ReportRules.FitsInLimit(x.From, x.To)).WithMessage(ReportRules.TooLong)
            .When(x => x.To >= x.From);
    }
}

public class GetProductRankingOptionsValidator : AbstractValidator<GetProductRankingOptions>
{
    public GetProductRankingOptionsValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage(ReportRules.ToBeforeFrom);
        RuleFor(x => x)
            .Must(x => ReportRules.FitsInLimit(x.From, x.To)).WithMessage(ReportRules.TooLong)
            .When(x => x.To >= x.From);
    }
}

public class GetCostChangesOptionsValidator : AbstractValidator<GetCostChangesOptions>
{
    public GetCostChangesOptionsValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage(ReportRules.ToBeforeFrom);
        RuleFor(x => x)
            .Must(x => ReportRules.FitsInLimit(x.From, x.To)).WithMessage(ReportRules.TooLong)
            .When(x => x.To >= x.From);
    }
}

/// <summary>The cost history of ONE product has no length limit: it is a handful of purchases a year.</summary>
public class GetProductCostHistoryOptionsValidator : AbstractValidator<GetProductCostHistoryOptions>
{
    public GetProductCostHistoryOptionsValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage(ReportRules.ToBeforeFrom)
            .When(x => x.From is not null && x.To is not null);
    }
}

public class GetMonthlyReportOptionsValidator : AbstractValidator<GetMonthlyReportOptions>
{
    public GetMonthlyReportOptionsValidator()
    {
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes va de 1 a 12.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).WithMessage("El año no es válido.");
        RuleFor(x => x)
            .Must(x => new DateOnly(x.Year, x.Month, 1) <= x.Today)
            .WithMessage("Ese mes todavía no empezó.")
            .When(x => x.Month is >= 1 and <= 12 && x.Year is >= 2000 and <= 2100);
    }
}
