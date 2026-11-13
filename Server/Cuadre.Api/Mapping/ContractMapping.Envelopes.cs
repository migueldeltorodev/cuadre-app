using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 5: Profit First envelopes, allocation plans, envelope operations and the envelope ledger.</summary>
public static partial class ContractMapping
{
    // ---------- Envelopes ----------

    public static Envelope MapToEnvelope(this CreateEnvelopeRequest request)
    {
        return new Envelope
        {
            Id = Guid.NewGuid(),
            Name = request.Name?.Trim() ?? "",
            Role = EnvelopeRole.Custom,
            IsActive = true
        };
    }

    public static EnvelopeChanges MapToEnvelopeChanges(this UpdateEnvelopeRequest request, Guid id)
    {
        return new EnvelopeChanges(id, request.Name?.Trim() ?? "", request.IsActive);
    }

    public static EnvelopeResponse MapToResponse(this Envelope envelope)
    {
        return new EnvelopeResponse
        {
            Id = envelope.Id,
            Name = envelope.Name,
            Role = envelope.Role.ToContract(),
            SortOrder = envelope.SortOrder,
            IsActive = envelope.IsActive,
            TakesPercent = envelope.TakesPercent
        };
    }

    public static EnvelopesResponse MapToResponse(this IEnumerable<Envelope> envelopes)
    {
        return new EnvelopesResponse { Items = envelopes.Select(MapToResponse) };
    }

    // ---------- Allocation plans ----------

    public static AllocationPlan MapToAllocationPlan(this CreateAllocationPlanRequest request)
    {
        var planId = Guid.NewGuid();
        return new AllocationPlan
        {
            Id = planId,
            EffectiveFrom = request.EffectiveFrom,
            Notes = BlankToNull(request.Notes),
            Items = (request.Items ?? []).Select(item => new AllocationPlanItem
            {
                PlanId = planId,
                EnvelopeId = item.EnvelopeId,
                PercentBps = item.PercentBps
            }).ToList()
        };
    }

    public static AllocationPlanResponse MapToResponse(this AllocationPlan plan)
    {
        return new AllocationPlanResponse
        {
            Id = plan.Id,
            EffectiveFrom = plan.EffectiveFrom,
            Notes = plan.Notes,
            Items = plan.Items.Select(item => new AllocationPlanItemResponse
            {
                EnvelopeId = item.EnvelopeId,
                EnvelopeName = item.EnvelopeName,
                PercentBps = item.PercentBps
            }).ToList(),
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
    }

    public static AllocationPlansResponse MapToResponse(this IEnumerable<AllocationPlan> plans)
    {
        return new AllocationPlansResponse { Items = plans.Select(MapToResponse) };
    }

    // ---------- Balances and ledger ----------

    public static EnvelopeOverviewResponse MapToResponse(this EnvelopeOverview overview)
    {
        return new EnvelopeOverviewResponse
        {
            Date = overview.Date,
            CashBoxes = overview.CashBoxes.Select(box => new EnvelopeCashBoxResponse
            {
                Id = box.Id,
                Name = box.Name,
                Currency = box.Currency.ToContract(),
                Kind = box.Kind.ToContract(),
                IsActive = box.IsActive,
                TotalCents = box.TotalCents
            }).ToList(),
            Envelopes = overview.Envelopes.Select(row => new EnvelopeBalanceResponse
            {
                Envelope = row.Envelope.MapToResponse(),
                PercentBps = row.PercentBps,
                Balances = row.Balances.MapToResponse()
            }).ToList(),
            CurrentPlan = overview.CurrentPlan?.MapToResponse(),
            InternalDebtCents = overview.InternalDebtCents
        };
    }

    public static GetEnvelopeLedgerOptions MapToOptions(this GetEnvelopeLedgerRequest request, Guid envelopeId)
    {
        return new GetEnvelopeLedgerOptions
        {
            EnvelopeId = envelopeId,
            From = request.From,
            To = request.To,
            CashBoxId = request.CashBoxId
        };
    }

    public static EnvelopeLedgerResponse MapToResponse(this EnvelopeLedger ledger, GetEnvelopeLedgerOptions options)
    {
        return new EnvelopeLedgerResponse
        {
            Envelope = ledger.Envelope.MapToResponse(),
            From = options.From,
            To = options.To,
            OpeningBalances = ledger.OpeningBalances.MapToResponse(),
            Entries = ledger.Entries.Select(entry => new EnvelopeLedgerEntryResponse
            {
                Movement = entry.Movement.MapToResponse(),
                BoxBalanceCents = entry.BoxBalanceCents
            }).ToList(),
            ClosingBalances = ledger.ClosingBalances.MapToResponse()
        };
    }

    private static CashMovementResponse MapToResponse(this CashMovementEntry movement)
    {
        return new CashMovementResponse
        {
            Id = movement.Id,
            CashBoxId = movement.CashBoxId,
            CashBoxName = movement.CashBoxName,
            Currency = movement.Currency.ToContract(),
            EnvelopeId = movement.EnvelopeId,
            EnvelopeName = movement.EnvelopeName,
            Date = movement.Date,
            Kind = movement.Kind.ToContract(),
            AmountCents = movement.AmountCents,
            Description = movement.Description,
            PurchaseId = movement.PurchaseId,
            PurchaseNumber = movement.PurchaseNumber,
            EnvelopeOperationId = movement.EnvelopeOperationId,
            EnvelopeOperationNumber = movement.EnvelopeOperationNumber,
            StockOperationId = movement.StockOperationId,
            StockOperationNumber = movement.StockOperationNumber,
            ClosingId = movement.ClosingId,
            ClosingDate = movement.ClosingDate,
            RecoveryId = movement.RecoveryId,
            RecoveryClosingId = movement.RecoveryClosingId,
            IsVoided = movement.IsVoided,
            CreatedAt = movement.CreatedAt
        };
    }

    private static List<EnvelopeBoxBalanceResponse> MapToResponse(this IEnumerable<EnvelopeBoxBalance> balances) =>
        balances.Select(balance => new EnvelopeBoxBalanceResponse
        {
            CashBoxId = balance.CashBoxId,
            AmountCents = balance.AmountCents
        }).ToList();

    // ---------- Envelope operations ----------

    public static EnvelopeOperation MapToEnvelopeOperation(this CreateEnvelopeOpeningRequest request)
    {
        return new EnvelopeOperation
        {
            Id = Guid.NewGuid(),
            Kind = EnvelopeOperationKind.Opening,
            Date = request.Date,
            Notes = BlankToNull(request.Notes),
            Targets = (request.Targets ?? [])
                .Select(target => new OpeningTarget(target.CashBoxId, target.EnvelopeId, target.AmountCents))
                .ToList()
        };
    }

    public static EnvelopeOperation MapToEnvelopeOperation(this CreateEnvelopeTransferRequest request)
    {
        return new EnvelopeOperation
        {
            Id = Guid.NewGuid(),
            Kind = EnvelopeOperationKind.Transfer,
            Date = request.Date,
            CashBoxId = request.CashBoxId,
            EnvelopeId = request.FromEnvelopeId,
            ToEnvelopeId = request.ToEnvelopeId,
            AmountCents = request.AmountCents,
            Notes = BlankToNull(request.Notes)
        };
    }

    public static EnvelopeOperation MapToEnvelopeOperation(this CreateEnvelopeWithdrawalRequest request)
    {
        return new EnvelopeOperation
        {
            Id = Guid.NewGuid(),
            Kind = EnvelopeOperationKind.Withdrawal,
            Date = request.Date,
            CashBoxId = request.CashBoxId,
            EnvelopeId = request.EnvelopeId,
            AmountCents = request.AmountCents,
            Notes = BlankToNull(request.Notes)
        };
    }

    public static EnvelopeOperation MapToEnvelopeOperation(this CreateTaxPaymentRequest request)
    {
        return new EnvelopeOperation
        {
            Id = Guid.NewGuid(),
            Kind = EnvelopeOperationKind.TaxPayment,
            Date = request.Date,
            CashBoxId = request.CashBoxId,
            AmountCents = request.AmountCents,
            Concept = BlankToNull(request.Concept),
            Receipt = BlankToNull(request.Receipt),
            Notes = BlankToNull(request.Notes)
        };
    }

    public static VoidEnvelopeOperation MapToVoidEnvelopeOperation(this VoidEnvelopeOperationRequest request, Guid id)
    {
        return new VoidEnvelopeOperation(id, request.Reason ?? "");
    }

    public static EnvelopeOperationResponse MapToResponse(this EnvelopeOperationResult result)
    {
        return result.Operation.MapToResponse(result.Warnings);
    }

    public static EnvelopeOperationResponse MapToResponse(this EnvelopeOperation operation,
        IEnumerable<NegativeEnvelopeWarning>? warnings = null)
    {
        return new EnvelopeOperationResponse
        {
            Id = operation.Id,
            Number = operation.Number,
            Kind = operation.Kind.ToContract(),
            Date = operation.Date,
            CashBoxId = operation.CashBoxId,
            CashBoxName = operation.CashBoxName,
            Currency = operation.Currency?.ToContract(),
            EnvelopeId = operation.EnvelopeId,
            EnvelopeName = operation.EnvelopeName,
            ToEnvelopeId = operation.ToEnvelopeId,
            ToEnvelopeName = operation.ToEnvelopeName,
            AmountCents = operation.AmountCents,
            Concept = operation.Concept,
            Receipt = operation.Receipt,
            Notes = operation.Notes,
            Status = operation.Status.ToContract(),
            VoidReason = operation.VoidReason,
            VoidedAt = operation.VoidedAt,
            Lines = operation.Lines.Select(line => new EnvelopeOperationLineResponse
            {
                Id = line.Id,
                LineNo = line.LineNo,
                CashBoxId = line.CashBoxId,
                CashBoxName = line.CashBoxName,
                Currency = line.Currency.ToContract(),
                EnvelopeId = line.EnvelopeId,
                EnvelopeName = line.EnvelopeName,
                AmountCents = line.AmountCents,
                TargetCents = line.TargetCents
            }).ToList(),
            Warnings = (warnings ?? []).Select(warning => new NegativeEnvelopeWarningResponse
            {
                EnvelopeId = warning.EnvelopeId,
                EnvelopeName = warning.EnvelopeName,
                CashBoxId = warning.CashBoxId,
                CashBoxName = warning.CashBoxName,
                Currency = warning.Currency.ToContract(),
                BalanceCents = warning.BalanceCents
            }).ToList(),
            CreatedAt = operation.CreatedAt,
            UpdatedAt = operation.UpdatedAt
        };
    }

    public static EnvelopeOperationsResponse MapToResponse(this IEnumerable<EnvelopeOperationSummary> operations,
        int page, int pageSize, int total)
    {
        return new EnvelopeOperationsResponse
        {
            Items = operations.Select(summary => new EnvelopeOperationSummaryResponse
            {
                Id = summary.Id,
                Number = summary.Number,
                Kind = summary.Kind.ToContract(),
                Date = summary.Date,
                CashBoxId = summary.CashBoxId,
                CashBoxName = summary.CashBoxName,
                Currency = summary.Currency?.ToContract(),
                EnvelopeId = summary.EnvelopeId,
                EnvelopeName = summary.EnvelopeName,
                ToEnvelopeId = summary.ToEnvelopeId,
                ToEnvelopeName = summary.ToEnvelopeName,
                AmountCents = summary.AmountCents,
                Concept = summary.Concept,
                LineCount = summary.LineCount,
                Status = summary.Status.ToContract()
            }),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static GetAllEnvelopeOperationsOptions MapToOptions(this GetAllEnvelopeOperationsRequest request)
    {
        return new GetAllEnvelopeOperationsOptions
        {
            Kind = request.Kind?.ToModel(),
            From = request.From,
            To = request.To,
            EnvelopeId = request.EnvelopeId,
            IncludeVoided = request.IncludeVoided.GetValueOrDefault(),
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    // ---------- Enums ----------

    private static EnvelopeType ToContract(this EnvelopeRole role) => role switch
    {
        EnvelopeRole.Profit => EnvelopeType.Profit,
        EnvelopeRole.OwnerPay => EnvelopeType.OwnerPay,
        EnvelopeRole.Tax => EnvelopeType.Tax,
        EnvelopeRole.Operations => EnvelopeType.Operations,
        EnvelopeRole.Replenishment => EnvelopeType.Replenishment,
        EnvelopeRole.Custom => EnvelopeType.Custom,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static EnvelopeOperationKind ToModel(this EnvelopeOperationType type) => type switch
    {
        EnvelopeOperationType.Opening => EnvelopeOperationKind.Opening,
        EnvelopeOperationType.Transfer => EnvelopeOperationKind.Transfer,
        EnvelopeOperationType.Withdrawal => EnvelopeOperationKind.Withdrawal,
        EnvelopeOperationType.TaxPayment => EnvelopeOperationKind.TaxPayment,
        _ => (EnvelopeOperationKind)(int)type
    };

    private static EnvelopeOperationType ToContract(this EnvelopeOperationKind kind) => kind switch
    {
        EnvelopeOperationKind.Opening => EnvelopeOperationType.Opening,
        EnvelopeOperationKind.Transfer => EnvelopeOperationType.Transfer,
        EnvelopeOperationKind.Withdrawal => EnvelopeOperationType.Withdrawal,
        EnvelopeOperationKind.TaxPayment => EnvelopeOperationType.TaxPayment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static EnvelopeOperationState ToContract(this EnvelopeOperationStatus status) => status switch
    {
        EnvelopeOperationStatus.Confirmed => EnvelopeOperationState.Confirmed,
        EnvelopeOperationStatus.Voided => EnvelopeOperationState.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static CashMovementType ToContract(this CashMovementKind kind) => kind switch
    {
        CashMovementKind.Purchase => CashMovementType.Purchase,
        CashMovementKind.PurchaseCharge => CashMovementType.PurchaseCharge,
        CashMovementKind.PurchaseVoid => CashMovementType.PurchaseVoid,
        CashMovementKind.Opening => CashMovementType.Opening,
        CashMovementKind.EnvelopeTransfer => CashMovementType.EnvelopeTransfer,
        CashMovementKind.Withdrawal => CashMovementType.Withdrawal,
        CashMovementKind.TaxPayment => CashMovementType.TaxPayment,
        CashMovementKind.HouseConsumption => CashMovementType.HouseConsumption,
        CashMovementKind.EnvelopeOperationVoid => CashMovementType.EnvelopeOperationVoid,
        CashMovementKind.StockOperationVoid => CashMovementType.StockOperationVoid,
        CashMovementKind.Collection => CashMovementType.Collection,
        CashMovementKind.Allocation => CashMovementType.Allocation,
        CashMovementKind.StaffPay => CashMovementType.StaffPay,
        CashMovementKind.ClosingVoid => CashMovementType.ClosingVoid,
        CashMovementKind.ShortageRecovery => CashMovementType.ShortageRecovery,
        CashMovementKind.ShortageRecoveryVoid => CashMovementType.ShortageRecoveryVoid,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
