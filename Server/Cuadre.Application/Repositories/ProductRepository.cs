using System.Data;
using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IProductRepository
{
    /// <summary>Inserts the product and, if it is born priced, its first history row: same transaction.</summary>
    Task<bool> CreateAsync(Product product, ProductPriceChange? priceChange,
        CancellationToken cancellationToken = default);

    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetAllAsync(GetAllProductsOptions options, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(GetAllProductsOptions options, CancellationToken cancellationToken = default);

    /// <summary>Every code starting with "PREFIX-", to compute the next one.</summary>
    Task<IEnumerable<string>> GetCodesWithPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Updates the product and, if the price changed, appends the history row: same transaction.</summary>
    Task<bool> UpdateAsync(Product product, ProductPriceChange? priceChange,
        CancellationToken cancellationToken = default);

    /// <summary>True once any stock movement exists: from then on the stock unit is frozen.</summary>
    Task<bool> HasStockMovementsAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Sets a new sale price and appends its history row, in one transaction.</summary>
    Task<bool> UpdateSalePriceAsync(Guid productId, long priceCents, DateTime updatedAt, ProductPriceChange priceChange,
        CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IEnumerable<ProductPriceChange>> GetPriceHistoryAsync(Guid productId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No delete: products are deactivated (RF-PRD-07), their history must survive.
/// listed_price_cents is NOT written here: only the price list owns it (PriceListRepository).
/// </summary>
public class ProductRepository : IProductRepository
{
    private const string SelectColumns = """
        id, code, barcode, name, brand, presentation, category_id, subcategory_id, usual_supplier_id,
        purchase_unit, stock_unit, conversion_factor_milli, sale_price_cents, listed_price_cents,
        min_stock_milli, shrinkage_tolerance_bps, exchange_buffer_cents, markup_bps,
        is_perishable, is_active, notes, created_at, updated_at,
        (SELECT COALESCE(SUM(m.quantity_milli), 0) FROM stock_movements m WHERE m.product_id = products.id) AS stock_milli,
        avg_cost_usd_micros, avg_cost_cup_micros, last_purchase_date
        """;

    // The cost cache's column names are unique on purpose, so the select list needs no table prefixes.
    private const string FromWithCosts =
        "products LEFT JOIN product_costs ON product_costs.product_id = products.id";

    // Shared by GetAll and GetCount so the total always matches the page.
    // ESCAPE: a search for "50%" must look for a literal %, not "50 followed by anything".
    private const string ListFilter = """
        (@search IS NULL
            OR code LIKE @pattern ESCAPE '\' OR name LIKE @pattern ESCAPE '\'
            OR brand LIKE @pattern ESCAPE '\' OR barcode LIKE @pattern ESCAPE '\')
        AND (@categoryId IS NULL OR category_id = @categoryId OR subcategory_id = @categoryId)
        AND (@includeInactive = 1 OR is_active = 1)
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public ProductRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Product product, ProductPriceChange? priceChange,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO products (
                id, code, barcode, name, brand, presentation, category_id, subcategory_id, usual_supplier_id,
                purchase_unit, stock_unit, conversion_factor_milli, sale_price_cents, listed_price_cents,
                min_stock_milli, shrinkage_tolerance_bps, exchange_buffer_cents, markup_bps,
                is_perishable, is_active, notes, created_at, updated_at)
            VALUES (
                @Id, @Code, @Barcode, @Name, @Brand, @Presentation, @CategoryId, @SubcategoryId, @UsualSupplierId,
                @PurchaseUnit, @StockUnit, @ConversionFactorMilli, @SalePriceCents, NULL,
                @MinStockMilli, @ShrinkageToleranceBps, @ExchangeBufferCents, @MarkupBps,
                @IsPerishable, @IsActive, @Notes, @CreatedAt, @UpdatedAt)
            """, ToParameters(product), transaction, cancellationToken: cancellationToken));

        await InsertPriceChangeAsync(connection, transaction, priceChange, cancellationToken);

        transaction.Commit();
        return result > 0;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM {FromWithCosts} WHERE id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM {FromWithCosts} WHERE code = @code",
            new { code }, cancellationToken: cancellationToken));
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM {FromWithCosts} WHERE barcode = @barcode",
            new { barcode }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Product>> GetAllAsync(GetAllProductsOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Product>(new CommandDefinition($"""
            SELECT {SelectColumns}
            FROM {FromWithCosts}
            WHERE {ListFilter}
            ORDER BY is_active DESC, name COLLATE NOCASE, code
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllProductsOptions options, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM products WHERE {ListFilter}",
            ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<string>> GetCodesWithPrefixAsync(string prefix,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<string>(new CommandDefinition(
            @"SELECT code FROM products WHERE code LIKE @pattern ESCAPE '\'",
            new { pattern = EscapeLike(prefix) + "-%" }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Product product, ProductPriceChange? priceChange,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE products SET
                code = @Code,
                barcode = @Barcode,
                name = @Name,
                brand = @Brand,
                presentation = @Presentation,
                category_id = @CategoryId,
                subcategory_id = @SubcategoryId,
                usual_supplier_id = @UsualSupplierId,
                purchase_unit = @PurchaseUnit,
                stock_unit = @StockUnit,
                conversion_factor_milli = @ConversionFactorMilli,
                sale_price_cents = @SalePriceCents,
                min_stock_milli = @MinStockMilli,
                shrinkage_tolerance_bps = @ShrinkageToleranceBps,
                exchange_buffer_cents = @ExchangeBufferCents,
                markup_bps = @MarkupBps,
                is_perishable = @IsPerishable,
                is_active = @IsActive,
                notes = @Notes,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, ToParameters(product), transaction, cancellationToken: cancellationToken));

        await InsertPriceChangeAsync(connection, transaction, priceChange, cancellationToken);

        transaction.Commit();
        return result > 0;
    }

    public async Task<IEnumerable<ProductPriceChange>> GetPriceHistoryAsync(Guid productId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<ProductPriceChange>(new CommandDefinition("""
            SELECT id, product_id, price_cents, previous_price_cents, effective_date, reason, created_at
            FROM product_price_changes
            WHERE product_id = @productId
            ORDER BY created_at DESC, rowid DESC
            """, new { productId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> HasStockMovementsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM stock_movements WHERE product_id = @productId)",
            new { productId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateSalePriceAsync(Guid productId, long priceCents, DateTime updatedAt,
        ProductPriceChange priceChange, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var result = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE products SET sale_price_cents = @priceCents, updated_at = @updatedAt WHERE id = @productId",
            new { productId, priceCents, updatedAt }, transaction, cancellationToken: cancellationToken));

        await InsertPriceChangeAsync(connection, transaction, priceChange, cancellationToken);

        transaction.Commit();
        return result > 0;
    }

    private static async Task InsertPriceChangeAsync(IDbConnection connection, IDbTransaction transaction,
        ProductPriceChange? priceChange, CancellationToken cancellationToken)
    {
        if (priceChange is null)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO product_price_changes
                (id, product_id, price_cents, previous_price_cents, effective_date, reason, created_at)
            VALUES (@Id, @ProductId, @PriceCents, @PreviousPriceCents, @EffectiveDate, @Reason, @CreatedAt)
            """, new
        {
            priceChange.Id,
            priceChange.ProductId,
            priceChange.PriceCents,
            priceChange.PreviousPriceCents,
            priceChange.EffectiveDate,
            Reason = priceChange.Reason.ToString(),
            priceChange.CreatedAt
        }, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Enums go as text explicitly: Dapper would send them as integers.</summary>
    private static object ToParameters(Product product) => new
    {
        product.Id,
        product.Code,
        product.Barcode,
        product.Name,
        product.Brand,
        product.Presentation,
        product.CategoryId,
        product.SubcategoryId,
        product.UsualSupplierId,
        PurchaseUnit = product.PurchaseUnit.ToString(),
        StockUnit = product.StockUnit.ToString(),
        product.ConversionFactorMilli,
        product.SalePriceCents,
        product.MinStockMilli,
        product.ShrinkageToleranceBps,
        product.ExchangeBufferCents,
        product.MarkupBps,
        product.IsPerishable,
        product.IsActive,
        product.Notes,
        product.CreatedAt,
        product.UpdatedAt
    };

    private static object ToFilterParameters(GetAllProductsOptions options, bool paged)
    {
        var search = string.IsNullOrWhiteSpace(options.Search) ? null : options.Search.Trim();
        return new
        {
            search,
            pattern = search is null ? null : $"%{EscapeLike(search)}%",
            categoryId = options.CategoryId,
            includeInactive = options.IncludeInactive,
            pageSize = paged ? options.PageSize : 0,
            offset = paged ? (options.Page - 1) * options.PageSize : 0
        };
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
