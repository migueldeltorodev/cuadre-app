namespace Cuadre.Application.Models;

/// <summary>
/// Informal USD->CUP rate of one calendar day (elTOQUE reference, typed by hand). One per date (RF-MON-02).
/// 1 USD = 410.50 CUP is stored as CupPerUsdCents = 41050.
/// </summary>
public class ExchangeRate
{
    public required DateOnly Date { get; init; }
    public required long CupPerUsdCents { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// What the app shows as "today's rate": the latest rate on or before today.
/// IsMissing drives the "no rate loaded today" warning (RF-MON-03).
/// </summary>
public sealed record CurrentExchangeRate(DateOnly Today, ExchangeRate? Latest)
{
    public bool IsMissing => Latest is null || Latest.Date != Today;
}

public class GetAllExchangeRatesOptions
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
