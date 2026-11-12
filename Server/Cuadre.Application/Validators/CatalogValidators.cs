using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Shape rules only (validators are singletons). Existence / uniqueness / "can it go back to null"
// depend on the database or on the previous state, so they live in the services.

public class CategoryValidator : AbstractValidator<Category>
{
    public CategoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(60);

        RuleFor(x => x.CodePrefix)
            .NotEmpty().WithMessage("La categoría necesita un prefijo para los códigos (ej. ARR).")
            .Matches("^[A-Z]{2,4}$").WithMessage("El prefijo son de 2 a 4 letras, sin tildes (ej. ARR).")
            .When(x => x.IsTopLevel);

        RuleFor(x => x.CodePrefix)
            .Null().WithMessage("Una subcategoría no lleva prefijo: usa el de su categoría.")
            .When(x => !x.IsTopLevel);
    }
}

public class SupplierValidator : AbstractValidator<Supplier>
{
    public SupplierValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proveedor es obligatorio.")
            .MaximumLength(80);

        RuleFor(x => x.ContactName).MaximumLength(80);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class ProductValidator : AbstractValidator<Product>
{
    private const long OneUnitMilli = 1000;
    private const long MaxFactorMilli = 100_000 * OneUnitMilli;   // 100,000 stock units per purchase unit
    private const long MaxPriceCents = 10_000_000_00;             // 10 million CUP: catches typos only

    public ProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("El código solo admite letras, números y guiones.");

        RuleFor(x => x.Barcode)
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9]+$").WithMessage("El código de barras solo admite letras y números.")
            .When(x => x.Barcode is not null);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del producto es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Brand).MaximumLength(60);
        RuleFor(x => x.Presentation).MaximumLength(60);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Elegí una categoría.");
        RuleFor(x => x.SubcategoryId).NotEqual(Guid.Empty).When(x => x.SubcategoryId is not null);
        RuleFor(x => x.UsualSupplierId).NotEqual(Guid.Empty).When(x => x.UsualSupplierId is not null);

        RuleFor(x => x.PurchaseUnit).IsInEnum();
        RuleFor(x => x.StockUnit).IsInEnum();

        RuleFor(x => x.ConversionFactorMilli)
            .GreaterThan(0).WithMessage("El factor de conversión tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxFactorMilli).WithMessage("El factor de conversión parece mal escrito.");

        // Same unit both ways ("compro por unidad, vendo por unidad") => the factor can only be 1.
        RuleFor(x => x.ConversionFactorMilli)
            .Equal(OneUnitMilli)
            .When(x => x.PurchaseUnit == x.StockUnit)
            .WithMessage("Si se compra y se vende en la misma unidad, el factor es 1.");

        // A box of 24 units holds 24 units, never 24.5.
        RuleFor(x => x.ConversionFactorMilli)
            .Must(BeWhole)
            .When(x => !x.StockUnit.AllowsDecimals())
            .WithMessage("Esta unidad no admite decimales: el factor tiene que ser un número entero.");

        RuleFor(x => x.SalePriceCents)
            .GreaterThan(0L).WithMessage("El precio de venta tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxPriceCents).WithMessage("El precio parece mal escrito.")
            .When(x => x.SalePriceCents is not null);

        RuleFor(x => x.MinStockMilli).GreaterThanOrEqualTo(0L);
        RuleFor(x => x.MinStockMilli)
            .Must(BeWhole)
            .When(x => !x.StockUnit.AllowsDecimals())
            .WithMessage("Esta unidad no admite decimales: el stock mínimo tiene que ser entero.");

        RuleFor(x => x.ShrinkageToleranceBps)
            .InclusiveBetween(0, 10_000).WithMessage("La tolerancia de merma va de 0 % a 100 %.");

        RuleFor(x => x.ExchangeBufferCents).GreaterThanOrEqualTo(0L).When(x => x.ExchangeBufferCents is not null);
        RuleFor(x => x.MarkupBps)
            .InclusiveBetween(0, 100_000).WithMessage("El recargo va de 0 % a 1000 %.")
            .When(x => x.MarkupBps is not null);
    }

    private static bool BeWhole(long milli) => milli % OneUnitMilli == 0;
}

public class GetAllProductsOptionsValidator : AbstractValidator<GetAllProductsOptions>
{
    public GetAllProductsOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
