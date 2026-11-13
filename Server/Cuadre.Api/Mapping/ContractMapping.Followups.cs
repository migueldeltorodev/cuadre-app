using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 6b: shortages and their recoveries, days left to close and the Profit First evaluation.</summary>
public static partial class ContractMapping
{
    // ---------- Shortages and surpluses ----------

    public static GetAllCashDiscrepanciesOptions MapToOptions(this GetAllCashDiscrepanciesRequest request)
    {
        return new GetAllCashDiscrepanciesOptions
        {
            Kind = request.Kind?.ToModel(),
            Status = request.Status?.ToModel(),
            EmployeeId = request.EmployeeId,
            From = request.From,
            To = request.To,
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    public static RecoverShortage MapToRecoverShortage(this RecoverShortageRequest request, Guid id)
    {
        return new RecoverShortage(id, request.Date, request.CashBoxId, request.AmountCents, request.Note);
    }

    public static WriteOffShortage MapToWriteOffShortage(this WriteOffShortageRequest request, Guid id)
    {
        return new WriteOffShortage(id, request.Note ?? "");
    }

    public static VoidShortageRecovery MapToVoidShortageRecovery(this VoidShortageRecoveryRequest request, Guid id)
    {
        return new VoidShortageRecovery(id, request.Reason ?? "");
    }

    public static CashDiscrepancyResponse MapToResponse(this CashDiscrepancy discrepancy)
    {
        return new CashDiscrepancyResponse
        {
            Id = discrepancy.Id,
            ClosingId = discrepancy.ClosingId,
            Date = discrepancy.Date,
            Kind = discrepancy.Kind.ToContract(),
            AmountCents = discrepancy.AmountCents,
            EmployeeId = discrepancy.EmployeeId,
            EmployeeName = discrepancy.EmployeeName,
            Note = discrepancy.Note,
            Status = discrepancy.Status.ToContract(),
            RecoveredCents = discrepancy.RecoveredCents,
            PendingCents = discrepancy.PendingCents,
            ResolutionNote = discrepancy.ResolutionNote,
            ResolvedAt = discrepancy.ResolvedAt,
            Recoveries = discrepancy.Recoveries.Select(recovery => new ShortageRecoveryResponse
            {
                Id = recovery.Id,
                Date = recovery.Date,
                CashBoxId = recovery.CashBoxId,
                CashBoxName = recovery.CashBoxName,
                AmountCents = recovery.AmountCents,
                Note = recovery.Note,
                Status = recovery.Status.ToContract(),
                VoidReason = recovery.VoidReason,
                VoidedAt = recovery.VoidedAt,
                CreatedAt = recovery.CreatedAt
            }).ToList(),
            CreatedAt = discrepancy.CreatedAt,
            UpdatedAt = discrepancy.UpdatedAt
        };
    }

    public static CashDiscrepanciesResponse MapToResponse(this IEnumerable<CashDiscrepancy> discrepancies, int page,
        int pageSize, int total)
    {
        return new CashDiscrepanciesResponse
        {
            Items = discrepancies.Select(MapToResponse),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static PendingShortagesResponse MapToPendingShortagesResponse(this (int Count, long PendingCents) totals)
    {
        return new PendingShortagesResponse { Count = totals.Count, PendingCents = totals.PendingCents };
    }

    // ---------- Days left to close ----------

    public static PendingClosingDaysResponse MapToResponse(this PendingClosingDays pending)
    {
        return new PendingClosingDaysResponse
        {
            LastClosedDate = pending.LastClosedDate,
            Count = pending.Count,
            Days = pending.Days
        };
    }

    // ---------- Evaluation ----------

    public static ProfitFirstEvaluationResponse MapToResponse(this ProfitFirstEvaluation evaluation)
    {
        return new ProfitFirstEvaluationResponse
        {
            From = evaluation.From,
            To = evaluation.To,
            ClosedDays = evaluation.ClosedDays,
            CashCollectedCents = evaluation.CashCollectedCents,
            TransferCollectedCents = evaluation.TransferCollectedCents,
            CollectedCents = evaluation.CollectedCents,
            CostOfSalesCents = evaluation.CostOfSalesCents,
            RealIncomeCents = evaluation.RealIncomeCents,
            CostRatioBps = evaluation.CostRatioBps,
            RealIncomeRatioBps = evaluation.RealIncomeRatioBps,
            TheoreticalSalesCents = evaluation.TheoreticalSalesCents,
            StaffPayCents = evaluation.StaffPayCents,
            ShortageCents = evaluation.ShortageCents,
            SurplusCents = evaluation.SurplusCents,
            RecoveredCents = evaluation.RecoveredCents,
            InternalDebtCents = evaluation.InternalDebtCents,
            Envelopes = evaluation.Envelopes.Select(envelope => new EnvelopeEvaluationResponse
            {
                EnvelopeId = envelope.EnvelopeId,
                Name = envelope.Name,
                Role = envelope.Role.ToContract(),
                TargetPercentBps = envelope.TargetPercentBps,
                ReceivedCents = envelope.ReceivedCents,
                RealPercentBps = envelope.RealPercentBps
            }).ToList()
        };
    }

    // ---------- Enums ----------

    private static ShortageRecoveryState ToContract(this ShortageRecoveryStatus status) => status switch
    {
        ShortageRecoveryStatus.Confirmed => ShortageRecoveryState.Confirmed,
        ShortageRecoveryStatus.Voided => ShortageRecoveryState.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static CashDiscrepancyKind ToModel(this CashDiscrepancyType kind) => kind switch
    {
        CashDiscrepancyType.Shortage => CashDiscrepancyKind.Shortage,
        CashDiscrepancyType.Surplus => CashDiscrepancyKind.Surplus,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static CashDiscrepancyStatus ToModel(this CashDiscrepancyState status) => status switch
    {
        CashDiscrepancyState.Pending => CashDiscrepancyStatus.Pending,
        CashDiscrepancyState.Recorded => CashDiscrepancyStatus.Recorded,
        CashDiscrepancyState.Voided => CashDiscrepancyStatus.Voided,
        CashDiscrepancyState.Recovered => CashDiscrepancyStatus.Recovered,
        CashDiscrepancyState.Lost => CashDiscrepancyStatus.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
