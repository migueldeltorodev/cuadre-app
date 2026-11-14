namespace Cuadre.Contracts.Requests;

// Slice 8. Every filter is optional: the server fills the defaults and echoes them in the response.

/// <summary>RF-REP-05. Default: this month (up to today).</summary>
public class GetMonthlyReportRequest
{
    public int? Year { get; init; }
    public int? Month { get; init; }
}

/// <summary>RF-REP-08. Default: from the first day of this month to today.</summary>
public class GetLossReportRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>RF-REP-04. Default: this month. CategoryId takes its subcategories too.</summary>
public class GetProductRankingRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CategoryId { get; init; }
}

/// <summary>RF-REP-02/03 for one product. Default: its whole history. SupplierId filters the chart only.</summary>
public class GetProductCostHistoryRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? SupplierId { get; init; }
}

/// <summary>RF-REP-02 for a category or a supplier. Default: the last 12 months.</summary>
public class GetCostChangesRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? SupplierId { get; init; }
}
