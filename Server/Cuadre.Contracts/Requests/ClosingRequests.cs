using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

// Money in CUP cents (the closing only collects in CUP); quantities in thousandths of the stock unit.

public class GetDailyClosingDraftRequest
{
    /// <summary>Default: today.</summary>
    public DateOnly? Date { get; init; }
}

public class ClosingCountRequest
{
    public required Guid ProductId { get; init; }
    public required Guid LocationId { get; init; }

    /// <summary>What is physically on that counter at the end of the day.</summary>
    public required long CountedMilli { get; init; }
}

public class ClosingCollectionRequest
{
    public required Guid PaymentMethodId { get; init; }
    public required long AmountCents { get; init; }
}

/// <summary>
/// Used by both the preview (nothing saved) and the close. Every product of the draft must be counted; payment methods
/// left out count as 0. The server computes everything else.
/// </summary>
public class CreateDailyClosingRequest
{
    public required DateOnly Date { get; init; }
    public Guid? EmployeeId { get; init; }

    /// <summary>Required when the cash difference goes beyond the threshold (the preview says so).</summary>
    public string? Notes { get; init; }

    public required IReadOnlyList<ClosingCountRequest> Counts { get; init; } = [];
    public required IReadOnlyList<ClosingCollectionRequest> Collections { get; init; } = [];
}

public class ReopenDailyClosingRequest
{
    public required string Reason { get; init; }
}

public class GetAllDailyClosingsRequest : PagedRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool? IncludeVoided { get; init; }
}

/// <summary>Shortages and surpluses, newest first. Those of reopened closings are never listed.</summary>
public class GetAllCashDiscrepanciesRequest : PagedRequest
{
    public CashDiscrepancyType? Kind { get; init; }
    public CashDiscrepancyState? Status { get; init; }
    public Guid? EmployeeId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>Money that came back. It comes in that day (today by default) and is split like an A3.</summary>
public class RecoverShortageRequest
{
    public DateOnly? Date { get; init; }
    public required Guid CashBoxId { get; init; }
    public required long AmountCents { get; init; }
    public string? Note { get; init; }
}

/// <summary>What was not recovered is lost. Why is required.</summary>
public class WriteOffShortageRequest
{
    public required string Note { get; init; }
}

public class VoidShortageRecoveryRequest
{
    public required string Reason { get; init; }
}

/// <summary>RF-PF-10. Default: from the first day of this month to today.</summary>
public class GetProfitFirstEvaluationRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
