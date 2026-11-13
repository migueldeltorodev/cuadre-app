using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// RF-PF-10. Money in CUP cents; percentages and ratios in basis points.

public class EnvelopeEvaluationResponse
{
    public required Guid EnvelopeId { get; init; }
    public required string Name { get; init; }
    public required EnvelopeType Role { get; init; }

    /// <summary>The % of the plan in force at the end of the period. Null for Reposición.</summary>
    public int? TargetPercentBps { get; init; }

    /// <summary>Its split in the closings of the period plus the recoveries that came in.</summary>
    public required long ReceivedCents { get; init; }

    /// <summary>Its share of what the % envelopes received together. Null for Reposición or with nothing received.</summary>
    public int? RealPercentBps { get; init; }
}

public class ProfitFirstEvaluationResponse
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required int ClosedDays { get; init; }

    public required long CashCollectedCents { get; init; }
    public required long TransferCollectedCents { get; init; }

    /// <summary>A1.</summary>
    public required long CollectedCents { get; init; }

    /// <summary>A2.</summary>
    public required long CostOfSalesCents { get; init; }

    /// <summary>A3 = A1 − A2.</summary>
    public required long RealIncomeCents { get; init; }

    /// <summary>A2 / A1 and A3 / A1. Null without collections.</summary>
    public int? CostRatioBps { get; init; }

    public int? RealIncomeRatioBps { get; init; }

    public required long TheoreticalSalesCents { get; init; }
    public required long StaffPayCents { get; init; }
    public required long ShortageCents { get; init; }
    public required long SurplusCents { get; init; }

    /// <summary>Recoveries that came in during the period (of shortages of any day).</summary>
    public required long RecoveredCents { get; init; }

    /// <summary>What Reposición owes the others at the end of the period.</summary>
    public required long InternalDebtCents { get; init; }

    public required IReadOnlyList<EnvelopeEvaluationResponse> Envelopes { get; init; } = [];
}
