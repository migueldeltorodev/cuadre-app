namespace Cuadre.Api;

/// <summary>
/// Single source of truth for every route. Never inline route strings in endpoints.
/// Each feature slice adds its own nested class (Products, Purchases, DailyCloses...).
/// </summary>
public static class ApiEndpoints
{
    public const string ApiBase = "api";

    public static class App
    {
        private const string Base = $"{ApiBase}/app";

        public const string Info = $"{Base}/info";
    }

    /// <summary>Single resource (one settings row): no id in the route.</summary>
    public static class Settings
    {
        private const string Base = $"{ApiBase}/settings";

        public const string Get = Base;
        public const string Update = Base;
    }

    public static class CashBoxes
    {
        private const string Base = $"{ApiBase}/cash-boxes";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Update = $"{Base}/{{id:guid}}";
    }

    public static class PaymentMethods
    {
        private const string Base = $"{ApiBase}/payment-methods";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Update = $"{Base}/{{id:guid}}";
    }

    /// <summary>
    /// Keyed by calendar date (yyyy-MM-dd): one rate per day, so PUT is a natural create-or-correct.
    /// The literal "current" segment takes precedence over the {date} parameter in routing.
    /// </summary>
    public static class ExchangeRates
    {
        private const string Base = $"{ApiBase}/exchange-rates";

        public const string GetAll = Base;
        public const string Current = $"{Base}/current";
        public const string Get = $"{Base}/{{date}}";
        public const string Upsert = $"{Base}/{{date}}";
    }

    /// <summary>The only catalog resource with DELETE: allowed while nothing uses the category.</summary>
    public static class Categories
    {
        private const string Base = $"{ApiBase}/categories";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Update = $"{Base}/{{id:guid}}";
        public const string Delete = $"{Base}/{{id:guid}}";
    }

    public static class Suppliers
    {
        private const string Base = $"{ApiBase}/suppliers";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Update = $"{Base}/{{id:guid}}";

        /// <summary>What we buy from this supplier and at what cost (RF-PRV-02).</summary>
        public const string Products = $"{Base}/{{id:guid}}/products";
    }

    /// <summary>"next-code" is a literal segment, so it never collides with {id:guid}.</summary>
    public static class Products
    {
        private const string Base = $"{ApiBase}/products";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string NextCode = $"{Base}/next-code";
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Update = $"{Base}/{{id:guid}}";
        public const string PriceHistory = $"{Base}/{{id:guid}}/price-history";
        public const string Purchases = $"{Base}/{{id:guid}}/purchases";

        /// <summary>Only the price: accepting a suggestion must not resend the whole product.</summary>
        public const string SalePrice = $"{Base}/{{id:guid}}/sale-price";

        /// <summary>D-61: prices to look at (a new batch on sale, or the next one will not fit).</summary>
        public const string PriceNotices = $"{Base}/price-notices";

        /// <summary>D-61: take the suggested price or keep the price.</summary>
        public const string ResolvePriceNotice = $"{Base}/{{id:guid}}/price-notice";
    }

    /// <summary>The fixed list of measure units (read-only, defined in code).</summary>
    public static class Units
    {
        public const string GetAll = $"{ApiBase}/units";
    }

    public static class PriceList
    {
        private const string Base = $"{ApiBase}/price-list";

        public const string Get = Base;

        /// <summary>POST = "ya lo copié al cartel".</summary>
        public const string Copies = $"{Base}/copies";
    }

    /// <summary>No PUT and no DELETE: a confirmed purchase is only voided, with a reason (RF-CMP-07).</summary>
    public static class Purchases
    {
        private const string Base = $"{ApiBase}/purchases";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Void = $"{Base}/{{id:guid}}/void";
    }

    /// <summary>No DELETE: locations hold movements, so they are deactivated (RF-INV-01).</summary>
    public static class Locations
    {
        private const string Base = $"{ApiBase}/locations";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Update = $"{Base}/{{id:guid}}";
    }

    /// <summary>
    /// Transfers, exits and counts. One POST per kind (each has its own shape); one history and one void for all.
    /// Like purchases: no PUT, no DELETE.
    /// </summary>
    public static class StockOperations
    {
        private const string Base = $"{ApiBase}/stock-operations";

        public const string CreateTransfer = $"{Base}/transfers";
        public const string CreateExit = $"{Base}/exits";
        public const string CreateCount = $"{Base}/counts";
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Void = $"{Base}/{{id:guid}}/void";
    }

    /// <summary>Read-only views over the stock ledger.</summary>
    public static class Inventory
    {
        private const string Base = $"{ApiBase}/inventory";

        public const string Stock = $"{Base}/stock";
        public const string Movements = $"{Base}/movements";
        public const string ShoppingList = $"{Base}/shopping-list";
        public const string Kardex = $"{ApiBase}/products/{{id:guid}}/kardex";
    }

    /// <summary>
    /// Profit First envelopes. No DELETE: they hold movements, so only the user's own are deactivated.
    /// "balances" is a literal segment, so it never collides with {id:guid}.
    /// </summary>
    public static class Envelopes
    {
        private const string Base = $"{ApiBase}/envelopes";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Update = $"{Base}/{{id:guid}}";
        public const string Balances = $"{Base}/balances";
        public const string Movements = $"{Base}/{{id:guid}}/movements";
    }

    /// <summary>The % of each envelope from a date on. Only created: a change is a new plan (RF-PF-05).</summary>
    public static class AllocationPlans
    {
        private const string Base = $"{ApiBase}/allocation-plans";

        public const string Create = Base;
        public const string GetAll = Base;
    }

    /// <summary>Openings, moves between envelopes, withdrawals and tax payments. Like purchases: no PUT, no DELETE.</summary>
    public static class EnvelopeOperations
    {
        private const string Base = $"{ApiBase}/envelope-operations";

        public const string CreateOpening = $"{Base}/openings";
        public const string CreateTransfer = $"{Base}/transfers";
        public const string CreateWithdrawal = $"{Base}/withdrawals";
        public const string CreateTaxPayment = $"{Base}/tax-payments";
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Void = $"{Base}/{{id:guid}}/void";
    }

    /// <summary>The close of a day. Never edited: reopening voids it (only the latest closed day).</summary>
    public static class DailyClosings
    {
        private const string Base = $"{ApiBase}/daily-closings";

        public const string Draft = $"{Base}/draft";
        public const string Preview = $"{Base}/preview";
        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Reopen = $"{Base}/{{id:guid}}/reopen";

        /// <summary>RF-CIE-09: business days already over and not closed.</summary>
        public const string Pending = $"{Base}/pending";
    }

    /// <summary>Shortages and surpluses of the closings (RF-CIE-12/13). Created only by a closing.</summary>
    public static class CashDiscrepancies
    {
        private const string Base = $"{ApiBase}/cash-discrepancies";

        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string PendingSummary = $"{Base}/pending-summary";
        public const string Recover = $"{Base}/{{id:guid}}/recoveries";
        public const string WriteOff = $"{Base}/{{id:guid}}/write-off";
    }

    /// <summary>A recovery is never edited: voided, with reversals dated today.</summary>
    public static class ShortageRecoveries
    {
        private const string Base = $"{ApiBase}/shortage-recoveries";

        public const string Void = $"{Base}/{{id:guid}}/void";
    }

    /// <summary>RF-PF-10.</summary>
    public static class ProfitFirst
    {
        private const string Base = $"{ApiBase}/profit-first";

        public const string Evaluation = $"{Base}/evaluation";
    }

    /// <summary>RF-GAS-03. No DELETE: expenses point to them, so they are deactivated.</summary>
    public static class ExpenseCategories
    {
        private const string Base = $"{ApiBase}/expense-categories";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Update = $"{Base}/{{id:guid}}";
    }

    /// <summary>RF-GAS-02/04. Like purchases: no PUT, no DELETE; voided with a reason.</summary>
    public static class Expenses
    {
        private const string Base = $"{ApiBase}/expenses";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Get = $"{Base}/{{id:guid}}";
        public const string Void = $"{Base}/{{id:guid}}/void";
    }

    /// <summary>
    /// RF-GAS-01: the templates and their occurrences. "due" is a literal segment, so it never collides with
    /// {id:guid}. Confirming creates an expense; skipping moves no money.
    /// </summary>
    public static class RecurringExpenses
    {
        private const string Base = $"{ApiBase}/recurring-expenses";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Update = $"{Base}/{{id:guid}}";
        public const string Due = $"{Base}/due";
        public const string Confirm = $"{Base}/{{id:guid}}/confirm";
        public const string Skip = $"{Base}/{{id:guid}}/skip";
    }

    /// <summary>RF-REP-01: the home screen.</summary>
    public static class Dashboard
    {
        public const string Get = $"{ApiBase}/dashboard";
    }

    /// <summary>RF-REP-02..05, 07, 08. Read-only.</summary>
    public static class Reports
    {
        private const string Base = $"{ApiBase}/reports";

        public const string Monthly = $"{Base}/monthly";
        public const string Losses = $"{Base}/losses";
        public const string ProductRanking = $"{Base}/product-ranking";
        public const string CostChanges = $"{Base}/cost-changes";
        public const string ProductCosts = $"{Base}/products/{{id:guid}}/costs";
    }

    /// <summary>
    /// RF-DAT-01..06. A backup is a file, not a row: no {id}. A restore names the folder and the file name.
    /// "settings", "drives" and "restore" are literal segments.
    /// </summary>
    public static class Backups
    {
        private const string Base = $"{ApiBase}/backups";

        public const string Status = Base;
        public const string Create = Base;
        public const string Settings = $"{Base}/settings";
        public const string Drives = $"{Base}/drives";
        public const string Restore = $"{Base}/restore";
    }

    /// <summary>RF-DAT-08: the audit log. Read-only: it is written by the actions themselves.</summary>
    public static class Audit
    {
        public const string GetAll = $"{ApiBase}/audit";
    }

    /// <summary>Who works the counter. Never deleted, only deactivated.</summary>
    public static class Employees
    {
        private const string Base = $"{ApiBase}/employees";

        public const string Create = Base;
        public const string GetAll = Base;
        public const string Update = $"{Base}/{{id:guid}}";
    }

    /// <summary>Pay of the post from a date on.</summary>
    public static class StaffPaySchemes
    {
        private const string Base = $"{ApiBase}/staff-pay-schemes";

        public const string Create = Base;
        public const string GetAll = Base;
    }
}
