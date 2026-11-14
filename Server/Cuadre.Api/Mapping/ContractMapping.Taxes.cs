using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>D-62 and D-63: the taxes and the accountant summary.</summary>
public static partial class ContractMapping
{
    // ---------- Taxes ----------

    public static Tax MapToTax(this CreateTaxRequest request)
    {
        return new Tax
        {
            Id = Guid.NewGuid(),
            Name = request.Name?.Trim() ?? "",
            RateBps = request.RateBps,
            Base = request.Base.ToModel()
        };
    }

    public static TaxChanges MapToTaxChanges(this UpdateTaxRequest request, Guid id)
    {
        return new TaxChanges(id, request.Name?.Trim() ?? "", request.RateBps, request.Base.ToModel(),
            request.IsActive);
    }

    public static TaxResponse MapToResponse(this Tax tax)
    {
        return new TaxResponse
        {
            Id = tax.Id,
            Name = tax.Name,
            RateBps = tax.RateBps,
            Base = tax.Base.ToContract(),
            IsActive = tax.IsActive,
            CreatedAt = tax.CreatedAt,
            UpdatedAt = tax.UpdatedAt
        };
    }

    public static TaxesResponse MapToResponse(this IEnumerable<Tax> taxes)
    {
        return new TaxesResponse { Items = taxes.Select(tax => tax.MapToResponse()) };
    }

    // ---------- Accountant summary ----------

    public static AccountantSummaryResponse MapToResponse(this AccountantSummary summary)
    {
        return new AccountantSummaryResponse
        {
            Year = summary.Year,
            Month = summary.Month,
            From = summary.From,
            To = summary.To,
            ClosedDays = summary.ClosedDays,
            Income = summary.Income.Select(method => new IncomeByMethodResponse
            {
                PaymentMethodId = method.PaymentMethodId,
                Name = method.Name,
                IsTransfer = method.IsTransfer,
                AmountCents = method.AmountCents
            }).ToList(),
            IncomeCents = summary.IncomeCents,
            CostOfSalesCents = summary.CostOfSalesCents,
            BufferCents = summary.BufferCents,
            GrossProfitCents = summary.GrossProfitCents,
            Expenses = summary.Expenses.Select(total => new ConvertedExpenseTotalResponse
            {
                CategoryId = total.CategoryId,
                CategoryName = total.CategoryName,
                CupCents = total.CupCents,
                UsdCents = total.UsdCents,
                UsdAsCupCents = total.UsdAsCupCents,
                UsdWithoutRateCents = total.UsdWithoutRateCents,
                TotalCupCents = total.TotalCupCents
            }).ToList(),
            ExpensesCents = summary.ExpensesCents,
            UsdWithoutRateCents = summary.UsdWithoutRateCents,
            StaffPayCents = summary.StaffPayCents,
            MerchandiseLossCents = summary.MerchandiseLossCents,
            CountGainCents = summary.CountGainCents,
            LostShortageCents = summary.LostShortageCents,
            ProfitBeforeTaxesCents = summary.ProfitBeforeTaxesCents,
            Taxes = summary.Taxes.Select(tax => new TaxSuggestionResponse
            {
                TaxId = tax.TaxId,
                Name = tax.Name,
                RateBps = tax.RateBps,
                Base = tax.Base.ToContract(),
                BaseCents = tax.BaseCents,
                AmountCents = tax.AmountCents
            }).ToList(),
            TaxesCents = summary.TaxesCents,
            ProfitAfterTaxesCents = summary.ProfitAfterTaxesCents,
            Purchases = new PurchaseTotalsResponse
            {
                Count = summary.Purchases.Count,
                CupCents = summary.Purchases.CupCents,
                UsdCents = summary.Purchases.UsdCents,
                UsdAsCupCents = summary.Purchases.UsdAsCupCents,
                TotalCupCents = summary.Purchases.TotalCupCents
            },
            Inventory = summary.Inventory.Select(location => new LocationInventoryResponse
            {
                LocationId = location.LocationId,
                LocationName = location.LocationName,
                OpeningCents = location.OpeningCents,
                ClosingCents = location.ClosingCents
            }).ToList(),
            OpeningInventoryCents = summary.OpeningInventoryCents,
            ClosingInventoryCents = summary.ClosingInventoryCents
        };
    }

    // ---------- Enums ----------

    private static TaxBase ToModel(this TaxBaseCode code) => code switch
    {
        TaxBaseCode.Sales => TaxBase.Sales,
        TaxBaseCode.Profit => TaxBase.Profit,
        _ => (TaxBase)(int)code
    };

    private static TaxBaseCode ToContract(this TaxBase taxBase) => taxBase switch
    {
        TaxBase.Sales => TaxBaseCode.Sales,
        TaxBase.Profit => TaxBaseCode.Profit,
        _ => throw new ArgumentOutOfRangeException(nameof(taxBase), taxBase, null)
    };
}
