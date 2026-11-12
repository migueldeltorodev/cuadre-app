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
}
