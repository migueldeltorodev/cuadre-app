using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 4: locations, stock operations (transfer, exit, count) and the inventory views.</summary>
public static partial class ContractMapping
{
    // ---------- Locations ----------

    public static Location MapToLocation(this CreateLocationRequest request)
    {
        return new Location
        {
            Id = Guid.NewGuid(),
            Name = request.Name?.Trim() ?? "",
            Kind = request.Kind.ToModel(),
            IsActive = true
        };
    }

    public static LocationChanges MapToLocationChanges(this UpdateLocationRequest request, Guid id)
    {
        return new LocationChanges(id, request.Name?.Trim() ?? "", request.IsActive);
    }

    public static LocationResponse MapToResponse(this Location location)
    {
        return new LocationResponse
        {
            Id = location.Id,
            Name = location.Name,
            Kind = location.Kind.ToContract(),
            IsActive = location.IsActive
        };
    }

    // ---------- Stock operations ----------

    public static StockOperation MapToStockOperation(this CreateTransferRequest request)
    {
        return NewOperation(StockOperationKind.Transfer, request.Date, request.FromLocationId, request.ToLocationId,
            null, request.Notes, request.Lines);
    }

    public static StockOperation MapToStockOperation(this CreateExitRequest request)
    {
        return NewOperation(StockOperationKind.Exit, request.Date, request.LocationId, null, request.Reason.ToModel(),
            request.Notes, request.Lines);
    }

    public static StockOperation MapToStockOperation(this CreateCountRequest request)
    {
        return NewOperation(StockOperationKind.Count, request.Date, request.LocationId, null, null, request.Notes,
            request.Lines);
    }

    private static StockOperation NewOperation(StockOperationKind kind, DateOnly date, Guid locationId,
        Guid? toLocationId, ExitReason? exitReason, string? notes, IReadOnlyList<StockOperationLineRequest>? lines)
    {
        var operationId = Guid.NewGuid();
        var operation = new StockOperation
        {
            Id = operationId,
            Kind = kind,
            Date = date,
            LocationId = locationId,
            ToLocationId = toLocationId,
            ExitReason = exitReason,
            Notes = BlankToNull(notes)
        };

        operation.Lines.AddRange((lines ?? []).Select(line => new StockOperationLine
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            ProductId = line.ProductId,
            EnteredQuantityMilli = line.QuantityMilli,
            EnteredInPurchaseUnits = line.InPurchaseUnits
        }));

        return operation;
    }

    public static VoidStockOperation MapToVoidStockOperation(this VoidStockOperationRequest request, Guid id)
    {
        return new VoidStockOperation(id, request.Reason ?? "");
    }

    public static StockOperationResponse MapToResponse(this StockOperationResult result)
    {
        return result.Operation.MapToResponse(result.Warnings);
    }

    public static StockOperationResponse MapToResponse(this StockOperation operation,
        IEnumerable<NegativeStockWarning>? warnings = null)
    {
        return new StockOperationResponse
        {
            Id = operation.Id,
            Number = operation.Number,
            Kind = operation.Kind.ToContract(),
            Date = operation.Date,
            LocationId = operation.LocationId,
            LocationName = operation.LocationName,
            ToLocationId = operation.ToLocationId,
            ToLocationName = operation.ToLocationName,
            ExitReason = operation.ExitReason?.ToContract(),
            Notes = operation.Notes,
            Status = operation.Status.ToContract(),
            VoidReason = operation.VoidReason,
            VoidedAt = operation.VoidedAt,
            Lines = operation.Lines.Select(line => new StockOperationLineResponse
            {
                Id = line.Id,
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                ProductCode = line.ProductCode,
                PurchaseUnit = line.PurchaseUnit.ToContract(),
                StockUnit = line.StockUnit.ToContract(),
                EnteredQuantityMilli = line.EnteredQuantityMilli,
                EnteredInPurchaseUnits = line.EnteredInPurchaseUnits,
                ConversionFactorMilli = line.ConversionFactorMilli,
                QuantityMilli = line.QuantityMilli,
                ExpectedMilli = line.ExpectedMilli,
                CountedMilli = line.CountedMilli,
                UnitCostUsdMicros = line.UnitCostUsdMicros,
                UnitCostCupMicros = line.UnitCostCupMicros,
                ValueCupCents = line.ValueCupCents
            }).ToList(),
            Warnings = (warnings ?? []).Select(warning => new NegativeStockWarningResponse
            {
                ProductId = warning.ProductId,
                ProductName = warning.ProductName,
                StockUnit = warning.StockUnit.ToContract(),
                LocationId = warning.LocationId,
                LocationName = warning.LocationName,
                BalanceMilli = warning.BalanceMilli
            }).ToList(),
            CreatedAt = operation.CreatedAt,
            UpdatedAt = operation.UpdatedAt
        };
    }

    public static StockOperationsResponse MapToResponse(this IEnumerable<StockOperationSummary> operations,
        int page, int pageSize, int total)
    {
        return new StockOperationsResponse
        {
            Items = operations.Select(summary => new StockOperationSummaryResponse
            {
                Id = summary.Id,
                Number = summary.Number,
                Kind = summary.Kind.ToContract(),
                Date = summary.Date,
                LocationId = summary.LocationId,
                LocationName = summary.LocationName,
                ToLocationId = summary.ToLocationId,
                ToLocationName = summary.ToLocationName,
                ExitReason = summary.ExitReason?.ToContract(),
                LineCount = summary.LineCount,
                ProductNames = summary.ProductNames,
                ValueCupCents = summary.ValueCupCents,
                Status = summary.Status.ToContract()
            }),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static GetAllStockOperationsOptions MapToOptions(this GetAllStockOperationsRequest request)
    {
        return new GetAllStockOperationsOptions
        {
            Kind = request.Kind?.ToModel(),
            From = request.From,
            To = request.To,
            LocationId = request.LocationId,
            ProductId = request.ProductId,
            IncludeVoided = request.IncludeVoided.GetValueOrDefault(),
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    // ---------- Inventory views ----------

    public static GetStockOptions MapToOptions(this GetStockRequest request)
    {
        return new GetStockOptions
        {
            Search = request.Search,
            CategoryId = request.CategoryId,
            LowOnly = request.LowOnly.GetValueOrDefault(),
            IncludeInactive = request.IncludeInactive.GetValueOrDefault()
        };
    }

    public static StockResponse MapToResponse(this StockOverview overview)
    {
        return new StockResponse
        {
            Locations = overview.Locations.Select(location => new StockLocationResponse
            {
                Id = location.Id,
                Name = location.Name,
                Kind = location.Kind.ToContract(),
                LastCountDate = location.LastCountDate
            }).ToList(),
            Items = overview.Products.Select(product => new ProductStockResponse
            {
                ProductId = product.ProductId,
                Code = product.Code,
                Name = product.Name,
                CategoryId = product.CategoryId,
                PurchaseUnit = product.PurchaseUnit.ToContract(),
                StockUnit = product.StockUnit.ToContract(),
                ConversionFactorMilli = product.ConversionFactorMilli,
                MinStockMilli = product.MinStockMilli,
                IsActive = product.IsActive,
                Locations = product.Locations.MapToResponse(),
                TotalMilli = product.TotalMilli,
                IsLow = product.IsLow
            }).ToList()
        };
    }

    public static GetKardexOptions MapToOptions(this GetKardexRequest request, Guid productId)
    {
        return new GetKardexOptions
        {
            ProductId = productId,
            From = request.From,
            To = request.To,
            LocationId = request.LocationId
        };
    }

    public static KardexResponse MapToResponse(this Kardex kardex, GetKardexOptions options)
    {
        return new KardexResponse
        {
            ProductId = kardex.Product.Id,
            ProductCode = kardex.Product.Code,
            ProductName = kardex.Product.Name,
            PurchaseUnit = kardex.Product.PurchaseUnit.ToContract(),
            StockUnit = kardex.Product.StockUnit.ToContract(),
            ConversionFactorMilli = kardex.Product.ConversionFactorMilli,
            From = options.From,
            To = options.To,
            OpeningBalances = kardex.OpeningBalances.MapToResponse(),
            Entries = kardex.Entries.Select(entry => new KardexEntryResponse
            {
                Movement = entry.Movement.MapToResponse(),
                LocationBalanceMilli = entry.LocationBalanceMilli,
                TotalBalanceMilli = entry.TotalBalanceMilli
            }).ToList(),
            ClosingBalances = kardex.ClosingBalances.MapToResponse()
        };
    }

    public static StockMovementsResponse MapToResponse(this IEnumerable<StockMovementEntry> movements, DateOnly date)
    {
        return new StockMovementsResponse
        {
            Date = date,
            Items = movements.Select(MapToResponse).ToList()
        };
    }

    public static ShoppingListResponse MapToResponse(this IEnumerable<ShoppingListItem> items)
    {
        return new ShoppingListResponse
        {
            Items = items.Select(item => new ShoppingListItemResponse
            {
                ProductId = item.ProductId,
                Code = item.Code,
                Name = item.Name,
                PurchaseUnit = item.PurchaseUnit.ToContract(),
                StockUnit = item.StockUnit.ToContract(),
                ConversionFactorMilli = item.ConversionFactorMilli,
                StockMilli = item.StockMilli,
                MinStockMilli = item.MinStockMilli,
                MissingMilli = item.MissingMilli,
                SuggestedPurchaseQuantityMilli = item.SuggestedPurchaseQuantityMilli,
                UsualSupplierId = item.UsualSupplierId,
                UsualSupplierName = item.UsualSupplierName,
                LastUnitCostUsdMicros = item.LastUnitCostUsdMicros,
                EstimatedUsdCents = item.EstimatedUsdCents
            }).ToList()
        };
    }

    private static StockMovementResponse MapToResponse(this StockMovementEntry movement)
    {
        return new StockMovementResponse
        {
            Id = movement.Id,
            ProductId = movement.ProductId,
            ProductName = movement.ProductName,
            StockUnit = movement.StockUnit.ToContract(),
            LocationId = movement.LocationId,
            LocationName = movement.LocationName,
            Date = movement.Date,
            Kind = movement.Kind.ToContract(),
            QuantityMilli = movement.QuantityMilli,
            UnitCostUsdMicros = movement.UnitCostUsdMicros,
            UnitCostCupMicros = movement.UnitCostCupMicros,
            PurchaseId = movement.PurchaseId,
            PurchaseNumber = movement.PurchaseNumber,
            OperationId = movement.OperationId,
            OperationNumber = movement.OperationNumber,
            ExitReason = movement.ExitReason?.ToContract(),
            ClosingId = movement.ClosingId,
            ClosingDate = movement.ClosingDate,
            IsVoided = movement.IsVoided,
            CreatedAt = movement.CreatedAt
        };
    }

    private static List<LocationQuantityResponse> MapToResponse(this IEnumerable<LocationQuantity> quantities) =>
        quantities.Select(quantity => new LocationQuantityResponse
        {
            LocationId = quantity.LocationId,
            QuantityMilli = quantity.QuantityMilli
        }).ToList();

    // ---------- Enums ----------

    private static LocationKind ToModel(this LocationType type) => type switch
    {
        LocationType.Storage => LocationKind.Storage,
        LocationType.Counter => LocationKind.Counter,
        _ => (LocationKind)(int)type
    };

    private static StockOperationKind ToModel(this StockOperationType type) => type switch
    {
        StockOperationType.Transfer => StockOperationKind.Transfer,
        StockOperationType.Exit => StockOperationKind.Exit,
        StockOperationType.Count => StockOperationKind.Count,
        _ => (StockOperationKind)(int)type
    };

    private static StockOperationType ToContract(this StockOperationKind kind) => kind switch
    {
        StockOperationKind.Transfer => StockOperationType.Transfer,
        StockOperationKind.Exit => StockOperationType.Exit,
        StockOperationKind.Count => StockOperationType.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static StockOperationState ToContract(this StockOperationStatus status) => status switch
    {
        StockOperationStatus.Confirmed => StockOperationState.Confirmed,
        StockOperationStatus.Voided => StockOperationState.Voided,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static ExitReason ToModel(this ExitReasonCode code) => code switch
    {
        ExitReasonCode.Shrinkage => ExitReason.Shrinkage,
        ExitReasonCode.Expired => ExitReason.Expired,
        ExitReasonCode.Damaged => ExitReason.Damaged,
        ExitReasonCode.HouseConsumption => ExitReason.HouseConsumption,
        ExitReasonCode.Adjustment => ExitReason.Adjustment,
        _ => (ExitReason)(int)code
    };

    private static ExitReasonCode ToContract(this ExitReason reason) => reason switch
    {
        ExitReason.Shrinkage => ExitReasonCode.Shrinkage,
        ExitReason.Expired => ExitReasonCode.Expired,
        ExitReason.Damaged => ExitReasonCode.Damaged,
        ExitReason.HouseConsumption => ExitReasonCode.HouseConsumption,
        ExitReason.Adjustment => ExitReasonCode.Adjustment,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    private static StockMovementType ToContract(this StockMovementKind kind) => kind switch
    {
        StockMovementKind.Purchase => StockMovementType.Purchase,
        StockMovementKind.PurchaseVoid => StockMovementType.PurchaseVoid,
        StockMovementKind.TransferOut => StockMovementType.TransferOut,
        StockMovementKind.TransferIn => StockMovementType.TransferIn,
        StockMovementKind.Exit => StockMovementType.Exit,
        StockMovementKind.CountAdjustment => StockMovementType.CountAdjustment,
        StockMovementKind.OperationVoid => StockMovementType.OperationVoid,
        StockMovementKind.Sale => StockMovementType.Sale,
        StockMovementKind.ClosingVoid => StockMovementType.ClosingVoid,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
