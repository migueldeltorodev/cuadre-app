using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 6: the daily closing, employees and the staff pay schemes.</summary>
public static partial class ContractMapping
{
    // ---------- Employees ----------

    public static Employee MapToEmployee(this CreateEmployeeRequest request)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            Name = request.Name?.Trim() ?? "",
            IsActive = true
        };
    }

    public static EmployeeChanges MapToEmployeeChanges(this UpdateEmployeeRequest request, Guid id)
    {
        return new EmployeeChanges(id, request.Name?.Trim() ?? "", request.IsActive);
    }

    public static EmployeeResponse MapToResponse(this Employee employee)
    {
        return new EmployeeResponse
        {
            Id = employee.Id,
            Name = employee.Name,
            IsActive = employee.IsActive,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }

    public static EmployeesResponse MapToResponse(this IEnumerable<Employee> employees)
    {
        return new EmployeesResponse { Items = employees.Select(MapToResponse) };
    }

    // ---------- Staff pay schemes ----------

    public static StaffPayScheme MapToStaffPayScheme(this CreateStaffPaySchemeRequest request)
    {
        return new StaffPayScheme
        {
            Id = Guid.NewGuid(),
            EffectiveFrom = request.EffectiveFrom,
            DailyFixedCents = request.DailyFixedCents,
            PercentOfSalesBps = request.PercentOfSalesBps,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };
    }

    public static StaffPaySchemeResponse MapToResponse(this StaffPayScheme scheme)
    {
        return new StaffPaySchemeResponse
        {
            Id = scheme.Id,
            EffectiveFrom = scheme.EffectiveFrom,
            DailyFixedCents = scheme.DailyFixedCents,
            PercentOfSalesBps = scheme.PercentOfSalesBps,
            Notes = scheme.Notes,
            CreatedAt = scheme.CreatedAt,
            UpdatedAt = scheme.UpdatedAt
        };
    }

    public static StaffPaySchemesResponse MapToResponse(this IEnumerable<StaffPayScheme> schemes)
    {
        return new StaffPaySchemesResponse { Items = schemes.Select(MapToResponse) };
    }

    // ---------- Daily closing ----------

    public static DailyClosingInput MapToDailyClosingInput(this CreateDailyClosingRequest request)
    {
        return new DailyClosingInput
        {
            Date = request.Date,
            EmployeeId = request.EmployeeId,
            Notes = request.Notes,
            Counts = (request.Counts ?? [])
                .Select(count => new ClosingCountInput(count.ProductId, count.LocationId, count.CountedMilli))
                .ToList(),
            Collections = (request.Collections ?? [])
                .Select(collection => new ClosingCollectionInput(collection.PaymentMethodId, collection.AmountCents))
                .ToList()
        };
    }

    public static ReopenDailyClosing MapToReopenDailyClosing(this ReopenDailyClosingRequest request, Guid id)
    {
        return new ReopenDailyClosing(id, request.Reason ?? "");
    }

    public static GetAllDailyClosingsOptions MapToOptions(this GetAllDailyClosingsRequest request)
    {
        return new GetAllDailyClosingsOptions
        {
            From = request.From,
            To = request.To,
            IncludeVoided = request.IncludeVoided.GetValueOrDefault(),
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    public static DailyClosingDraftResponse MapToResponse(this DailyClosingDraft draft)
    {
        return new DailyClosingDraftResponse
        {
            Date = draft.Date,
            IsClosedDay = draft.IsClosedDay,
            LastClosedDate = draft.LastClosedDate,
            ExistingClosingId = draft.ExistingClosingId,
            IsExchangeRateMissing = draft.IsExchangeRateMissing,
            Problem = draft.Problem,
            CanClose = draft.CanClose,
            Lines = draft.Lines.Select(line => new ClosingCounterLineResponse
            {
                ProductId = line.ProductId,
                Code = line.Code,
                Name = line.Name,
                StockUnit = line.StockUnit.ToContract(),
                AllowsDecimals = line.AllowsDecimals,
                LocationId = line.LocationId,
                LocationName = line.LocationName,
                OpeningMilli = line.OpeningMilli,
                InMilli = line.InMilli,
                OutMilli = line.OutMilli,
                ExpectedMilli = line.ExpectedMilli,
                UnitPriceCents = line.UnitPriceCents,
                ToleranceBps = line.ToleranceBps
            }).ToList(),
            PaymentMethods = draft.PaymentMethods.Select(method => new ClosingPaymentMethodResponse
            {
                Id = method.Id,
                Name = method.Name,
                CashBoxId = method.CashBoxId,
                CashBoxName = method.CashBoxName,
                CashBoxKind = method.CashBoxKind.ToContract(),
                IsTransfer = method.IsTransfer
            }).ToList(),
            PayScheme = draft.PayScheme.MapToResponse(),
            DebtBeforeCents = draft.DebtBeforeCents,
            ThresholdCents = draft.ThresholdCents
        };
    }

    public static DailyClosingResponse MapToResponse(this DailyClosing closing)
    {
        return new DailyClosingResponse
        {
            Id = closing.Id,
            Date = closing.Date,
            Status = closing.Status.ToContract(),
            EmployeeId = closing.EmployeeId,
            EmployeeName = closing.EmployeeName,
            Notes = closing.Notes,
            TheoreticalSalesCents = closing.TheoreticalSalesCents,
            ShrinkageToleranceCents = closing.ShrinkageToleranceCents,
            CashCollectedCents = closing.CashCollectedCents,
            TransferCollectedCents = closing.TransferCollectedCents,
            CollectedCents = closing.CollectedCents,
            CostOfSalesCents = closing.CostOfSalesCents,
            RealIncomeCents = closing.RealIncomeCents,
            CashDifferenceCents = closing.CashDifferenceCents,
            AdjustedDifferenceCents = closing.AdjustedDifferenceCents,
            ThresholdCents = closing.ThresholdCents,
            RequiresNote = closing.RequiresNote,
            StaffPayCents = closing.StaffPayCents,
            PayFixedCents = closing.PayFixedCents,
            PayPercentBps = closing.PayPercentBps,
            DebtBeforeCents = closing.DebtBeforeCents,
            DebtCreatedCents = closing.DebtCreatedCents,
            DebtRepaidCents = closing.DebtRepaidCents,
            DebtAfterCents = closing.DebtAfterCents,
            VoidReason = closing.VoidReason,
            VoidedAt = closing.VoidedAt,
            Lines = closing.Lines.OrderBy(line => line.LineNo).Select(line => new DailyClosingLineResponse
            {
                Id = line.Id,
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                ProductCode = line.ProductCode,
                ProductName = line.ProductName,
                LocationId = line.LocationId,
                LocationName = line.LocationName,
                StockUnit = line.StockUnit.ToContract(),
                OpeningMilli = line.OpeningMilli,
                InMilli = line.InMilli,
                OutMilli = line.OutMilli,
                ExpectedMilli = line.ExpectedMilli,
                CountedMilli = line.CountedMilli,
                SoldMilli = line.SoldMilli,
                UnitPriceCents = line.UnitPriceCents,
                SalesCents = line.SalesCents,
                ToleranceBps = line.ToleranceBps,
                ToleranceCents = line.ToleranceCents,
                CostCents = line.CostCents,
                BufferCents = line.BufferCents
            }).ToList(),
            Collections = closing.Collections.Select(collection => new DailyClosingCollectionResponse
            {
                PaymentMethodId = collection.PaymentMethodId,
                PaymentMethodName = collection.PaymentMethodName,
                CashBoxId = collection.CashBoxId,
                CashBoxName = collection.CashBoxName,
                IsTransfer = collection.IsTransfer,
                AmountCents = collection.AmountCents
            }).ToList(),
            Allocations = closing.Allocations
                .OrderBy(allocation => allocation.SortOrder)
                .Select(allocation => new DailyClosingAllocationResponse
                {
                    EnvelopeId = allocation.EnvelopeId,
                    EnvelopeName = allocation.EnvelopeName,
                    Role = allocation.Role.ToContract(),
                    PercentBps = allocation.PercentBps,
                    AmountCents = allocation.AmountCents,
                    CashCents = allocation.CashCents,
                    BankCents = allocation.BankCents
                }).ToList(),
            Discrepancy = closing.Discrepancy?.MapToResponse(),
            PossibleDuplicateOf = closing.PossibleDuplicateOf,
            CreatedAt = closing.CreatedAt,
            UpdatedAt = closing.UpdatedAt
        };
    }

    public static DailyClosingsResponse MapToResponse(this IEnumerable<DailyClosingSummary> closings, int page,
        int pageSize, int total)
    {
        return new DailyClosingsResponse
        {
            Items = closings.Select(summary => new DailyClosingSummaryResponse
            {
                Id = summary.Id,
                Date = summary.Date,
                Status = summary.Status.ToContract(),
                EmployeeName = summary.EmployeeName,
                CollectedCents = summary.CollectedCents,
                TransferCollectedCents = summary.TransferCollectedCents,
                CostOfSalesCents = summary.CostOfSalesCents,
                RealIncomeCents = summary.RealIncomeCents,
                AdjustedDifferenceCents = summary.AdjustedDifferenceCents,
                StaffPayCents = summary.StaffPayCents,
                DebtAfterCents = summary.DebtAfterCents
            }),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    // ---------- Enums ----------

    private static DailyClosingState ToContract(this DailyClosingStatus status) => status switch
    {
        DailyClosingStatus.Confirmed => DailyClosingState.Confirmed,
        DailyClosingStatus.Voided => DailyClosingState.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static CashDiscrepancyType ToContract(this CashDiscrepancyKind kind) => kind switch
    {
        CashDiscrepancyKind.Shortage => CashDiscrepancyType.Shortage,
        CashDiscrepancyKind.Surplus => CashDiscrepancyType.Surplus,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static CashDiscrepancyState ToContract(this CashDiscrepancyStatus status) => status switch
    {
        CashDiscrepancyStatus.Pending => CashDiscrepancyState.Pending,
        CashDiscrepancyStatus.Recorded => CashDiscrepancyState.Recorded,
        CashDiscrepancyStatus.Voided => CashDiscrepancyState.Voided,
        CashDiscrepancyStatus.Recovered => CashDiscrepancyState.Recovered,
        CashDiscrepancyStatus.Lost => CashDiscrepancyState.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
