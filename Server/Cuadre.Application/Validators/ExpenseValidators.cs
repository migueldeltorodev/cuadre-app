using Cuadre.Application.Models;
using FluentValidation;

namespace Cuadre.Application.Validators;

// Slice 7, shape rules only. Whether the category, the box or the envelope exist and are active, whether the day is
// open or an occurrence is really due depend on the database: the services check them (422).

public class ExpenseCategoryValidator : AbstractValidator<ExpenseCategory>
{
    public ExpenseCategoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Escribí el nombre de la categoría.")
            .MaximumLength(60);
    }
}

public class ExpenseValidator : AbstractValidator<Expense>
{
    private const long MaxAmountCents = 1_000_000_000_00L;   // catches typos only

    public ExpenseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Elegí la categoría.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Escribí qué se pagó.")
            .MaximumLength(200);
        RuleFor(x => x.AmountCents)
            .GreaterThan(0).WithMessage("El gasto tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.");
        RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí de qué caja sale.");
        RuleFor(x => x.EnvelopeId).NotEqual(Guid.Empty).When(x => x.EnvelopeId is not null);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.PeriodStart).NotNull().When(x => x.RecurringExpenseId is not null);
    }
}

public class VoidExpenseValidator : AbstractValidator<VoidExpense>
{
    public VoidExpenseValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escribí por qué se anula.")
            .MaximumLength(300);
    }
}

public class GetAllExpensesOptionsValidator : AbstractValidator<GetAllExpensesOptions>
{
    public GetAllExpensesOptionsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha 'hasta' no puede ser anterior a 'desde'.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public class RecurringExpenseValidator : AbstractValidator<RecurringExpense>
{
    private const long MaxAmountCents = 1_000_000_000_00L;

    public RecurringExpenseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Escribí el nombre del gasto fijo.")
            .MaximumLength(120);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Elegí la categoría.");
        RuleFor(x => x.AmountCents)
            .GreaterThan(0).WithMessage("El monto tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.");
        RuleFor(x => x.CashBoxId).NotEmpty().WithMessage("Elegí de qué caja sale.");
        RuleFor(x => x.EnvelopeId).NotEqual(Guid.Empty).When(x => x.EnvelopeId is not null);
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.DueDay)
            .InclusiveBetween(1, RecurringSchedule.MaxDayOfMonth)
            .WithMessage($"El día del mes va del 1 al {RecurringSchedule.MaxDayOfMonth} (todos los meses lo tienen).")
            .When(x => x.Frequency == RecurringFrequency.Monthly);
        RuleFor(x => x.DueDay)
            .InclusiveBetween(0, 6).WithMessage("Elegí el día de la semana.")
            .When(x => x.Frequency == RecurringFrequency.Weekly);
        RuleFor(x => x.StartsOn).NotEqual(default(DateOnly)).WithMessage("Elegí desde cuándo se paga.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class ConfirmRecurringExpenseValidator : AbstractValidator<ConfirmRecurringExpense>
{
    private const long MaxAmountCents = 1_000_000_000_00L;

    public ConfirmRecurringExpenseValidator()
    {
        RuleFor(x => x.PeriodStart).NotEqual(default(DateOnly)).WithMessage("Elegí qué período se paga.");
        RuleFor(x => x.AmountCents)
            .GreaterThan(0).WithMessage("El gasto tiene que ser mayor que cero.")
            .LessThanOrEqualTo(MaxAmountCents).WithMessage("El monto parece mal escrito.")
            .When(x => x.AmountCents is not null);
        RuleFor(x => x.CashBoxId).NotEqual(Guid.Empty).When(x => x.CashBoxId is not null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class SkipRecurringExpenseValidator : AbstractValidator<SkipRecurringExpense>
{
    public SkipRecurringExpenseValidator()
    {
        RuleFor(x => x.PeriodStart).NotEqual(default(DateOnly)).WithMessage("Elegí qué período se salta.");
    }
}
