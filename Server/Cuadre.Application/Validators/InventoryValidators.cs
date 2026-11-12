using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Shape rules only. Existence and active flags of locations and products, units and factors depend on the
// database, so the services check them (422).

public class LocationValidator : AbstractValidator<Location>
{
    public LocationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la ubicación es obligatorio.")
            .MaximumLength(40);
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public class StockOperationValidator : AbstractValidator<StockOperation>
{
    private const long MaxQuantityMilli = 1_000_000 * 1000L;   // catches typos only
    private const int MaxLines = 300;                          // a full warehouse count fits

    public StockOperationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Elegí la ubicación.");
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.ToLocationId)
            .NotEmpty().WithMessage("Elegí a dónde va la mercancía.")
            .When(x => x.Kind == StockOperationKind.Transfer);

        RuleFor(x => x.ToLocationId)
            .NotEqual(x => x.LocationId).WithMessage("El origen y el destino tienen que ser distintos.")
            .When(x => x.Kind == StockOperationKind.Transfer && x.ToLocationId is not null);

        RuleFor(x => x.ToLocationId)
            .Null().WithMessage("Solo un traslado tiene destino.")
            .When(x => x.Kind != StockOperationKind.Transfer);

        RuleFor(x => x.ExitReason)
            .NotNull().WithMessage("Elegí el motivo de la salida.")
            .IsInEnum()
            .When(x => x.Kind == StockOperationKind.Exit);

        RuleFor(x => x.ExitReason)
            .Null().WithMessage("Solo una salida lleva motivo.")
            .When(x => x.Kind != StockOperationKind.Exit);

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Agregá al menos un producto.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Se admiten hasta {MaxLines} productos.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.Id).NotEmpty();
            line.RuleFor(x => x.ProductId).NotEmpty().WithMessage("Elegí el producto.");
            line.RuleFor(x => x.EnteredQuantityMilli)
                .LessThanOrEqualTo(MaxQuantityMilli).WithMessage("La cantidad parece mal escrita.");
        });

        // A count may find nothing (0); moving or taking out nothing makes no sense.
        RuleForEach(x => x.Lines)
            .Must(line => line.EnteredQuantityMilli > 0)
            .WithMessage("La cantidad tiene que ser mayor que cero.")
            .When(x => x.Kind != StockOperationKind.Count);

        RuleForEach(x => x.Lines)
            .Must(line => line.EnteredQuantityMilli >= 0)
            .WithMessage("Lo contado no puede ser negativo.")
            .When(x => x.Kind == StockOperationKind.Count);
    }
}

public class VoidStockOperationValidator : AbstractValidator<VoidStockOperation>
{
    public VoidStockOperationValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribí por qué se anula.")
            .MaximumLength(300);
    }
}

public class GetAllStockOperationsOptionsValidator : AbstractValidator<GetAllStockOperationsOptions>
{
    public GetAllStockOperationsOptionsValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public class GetKardexOptionsValidator : AbstractValidator<GetKardexOptions>
{
    public GetKardexOptionsValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}
