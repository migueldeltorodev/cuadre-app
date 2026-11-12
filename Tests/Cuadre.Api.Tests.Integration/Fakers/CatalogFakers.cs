using Bogus;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Tests.Integration.Fakers;

public static class SupplierFaker
{
    public static CreateSupplierRequest CreateRequest() =>
        new Faker<CreateSupplierRequest>()
            .CustomInstantiator(f => new CreateSupplierRequest
            {
                Name = $"Proveedor {f.Random.AlphaNumeric(8)}",
                ContactName = f.Name.FullName(),
                Phone = f.Phone.PhoneNumber("5#######"),
                Notes = "Entrega los martes"
            })
            .Generate();
}

public static class ProductFaker
{
    /// <summary>
    /// "Arroz" style product: bought by the 100 lb sack, sold by the pound. No code (auto-generated).
    /// Contracts are init-only classes (no `with`), so the fields tests vary are parameters.
    /// </summary>
    public static CreateProductRequest CreateRequest(
        Guid categoryId,
        Guid? subcategoryId = null,
        Guid? supplierId = null,
        string? code = null,
        string? barcode = null,
        long? salePriceCents = 25_000,
        UnitCode purchaseUnit = UnitCode.Sack,
        UnitCode stockUnit = UnitCode.Pound,
        long conversionFactorMilli = 100_000,
        long minStockMilli = 20_000) => new()
    {
        Code = code,
        Barcode = barcode,
        Name = $"Producto {new Faker().Random.AlphaNumeric(8)}",
        Brand = "La Marca",
        Presentation = "Saco 100 lb",
        CategoryId = categoryId,
        SubcategoryId = subcategoryId,
        UsualSupplierId = supplierId,
        PurchaseUnit = purchaseUnit,
        StockUnit = stockUnit,
        ConversionFactorMilli = conversionFactorMilli,
        SalePriceCents = salePriceCents,
        MinStockMilli = minStockMilli,
        ShrinkageToleranceBps = 200,
        IsPerishable = false
    };

    /// <summary>The update request for a product as it is now, with the fields tests usually change.</summary>
    public static UpdateProductRequest UpdateRequest(
        ProductResponse product,
        long? salePriceCents = null,
        bool keepPrice = true,
        Guid? subcategoryId = null,
        Guid? supplierId = null,
        bool? isActive = null) => new()
    {
        Code = product.Code,
        Barcode = product.Barcode,
        Name = product.Name,
        Brand = product.Brand,
        Presentation = product.Presentation,
        CategoryId = product.CategoryId,
        SubcategoryId = subcategoryId ?? product.SubcategoryId,
        UsualSupplierId = supplierId ?? product.UsualSupplierId,
        PurchaseUnit = product.PurchaseUnit,
        StockUnit = product.StockUnit,
        ConversionFactorMilli = product.ConversionFactorMilli,
        SalePriceCents = salePriceCents ?? (keepPrice ? product.SalePriceCents : null),
        MinStockMilli = product.MinStockMilli,
        ShrinkageToleranceBps = product.ShrinkageToleranceBps,
        ExchangeBufferCents = product.ExchangeBufferCents,
        MarkupBps = product.MarkupBps,
        IsPerishable = product.IsPerishable,
        Notes = product.Notes,
        IsActive = isActive ?? product.IsActive
    };
}
