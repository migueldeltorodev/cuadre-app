namespace Cuadre.Api.Endpoints.Expenses;

/// <summary>Slice 7: Gastos, their categories (Configuración) and the recurring ones.</summary>
public static class ExpenseEndpointExtensions
{
    public static IEndpointRouteBuilder MapExpenseCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateExpenseCategory();
        app.MapGetAllExpenseCategories();
        app.MapUpdateExpenseCategory();
        return app;
    }

    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateExpense();
        app.MapGetAllExpenses();
        app.MapGetExpense();
        app.MapVoidExpense();
        return app;
    }

    public static IEndpointRouteBuilder MapRecurringExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateRecurringExpense();
        app.MapGetAllRecurringExpenses();
        app.MapUpdateRecurringExpense();
        app.MapGetDueExpenses();
        app.MapConfirmRecurringExpense();
        app.MapSkipRecurringExpense();
        return app;
    }
}
