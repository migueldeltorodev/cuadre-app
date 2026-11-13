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
}
