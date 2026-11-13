using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Shape rules only. Existence, active flags, currencies of the boxes, units and factors of the products
// depend on the database, so PurchaseService checks them (422).

public class PurchaseValidator : AbstractValidator<Purchase>
{
    private const long MaxQuantityMilli = 1_000_000 * 1000L;   // a million purchase units: catches typos only
    private const long MaxAmountCents = 1_000_000_000_00;      // a billion: catches typos only
    private const long MaxFactorMilli = 100_000 * 1000L;       // same limit as the product
    private const int MaxLines = 200;

    public PurchaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Elegí el proveedor.");
        RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí la caja con la que se pagó.");
        RuleFor(x => x.Currency).IsInEnum();

        RuleFor(x => x.RateCupPerUsdCents)
            .GreaterThan(0L).WithMessage("La tasa de la compra tiene que ser mayor que cero.")
            .LessThanOrEqualTo(100_000_00L).WithMessage("La tasa parece mal escrita.");

        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("La compra necesita al menos un producto.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Una compra admite hasta {MaxLines} productos.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.Id).NotEmpty();
            line.RuleFor(x => x.ProductId).NotEmpty().WithMessage("Elegí el producto.");

            line.RuleFor(x => x.QuantityMilli)
                .GreaterThan(0L).WithMessage("La cantidad tiene que ser mayor que cero.")
                .LessThanOrEqualTo(MaxQuantityMilli).WithMessage("La cantidad parece mal escrita.");

            line.RuleFor(x => x.UnitCostCents)
                .GreaterThan(0L).WithMessage("El costo tiene que ser mayor que cero.")
                .LessThanOrEqualTo(MaxAmountCents).WithMessage("El costo parece mal escrito.");

            line.RuleFor(x => x.ConversionFactorMilli)
                .GreaterThan(0L).WithMessage("El factor de conversión tiene que ser mayor que cero.")
                .LessThanOrEqualTo(MaxFactorMilli).WithMessage("El factor de conversión parece mal escrito.")
                .When(x => x.ConversionFactorMilli is not null);
        });

        RuleForEach(x => x.Charges).ChildRules(charge =>
        {
            charge.RuleFor(x => x.Id).NotEmpty();
            charge.RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Describí el gasto (flete, descarga...).")
                .MaximumLength(80);
            charge.RuleFor(x => x.Currency).IsInEnum();
            charge.RuleFor(x => x.AmountCents)
                .GreaterThan(0L).WithMessage("El importe del gasto tiene que ser mayor que cero.")
                .LessThanOrEqualTo(MaxAmountCents).WithMessage("El importe parece mal escrito.");
            charge.RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí la caja con la que se pagó el gasto.");
        });

        RuleFor(x => x.Charges)
            .Must(charges => charges.Count <= 20).WithMessage("Una compra admite hasta 20 gastos.");
    }
}

/// <summary>Input of a void: the reason is mandatory (RF-CMP-07).</summary>
public sealed record VoidPurchase(Guid PurchaseId, string Reason);

public class VoidPurchaseValidator : AbstractValidator<VoidPurchase>
{
    public VoidPurchaseValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribí por qué se anula la compra.")
            .MaximumLength(300);
    }
}

public class GetAllPurchasesOptionsValidator : AbstractValidator<GetAllPurchasesOptions>
{
    public GetAllPurchasesOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public class SetSalePriceValidator : AbstractValidator<SetSalePrice>
{
    public SetSalePriceValidator()
    {
        RuleFor(x => x.SalePriceCents)
            .GreaterThan(0L).WithMessage("El precio de venta tiene que ser mayor que cero.")
            .LessThanOrEqualTo(10_000_000_00L).WithMessage("El precio parece mal escrito.");
    }
}
